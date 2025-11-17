using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MesMiddleware.LabViewBridge
{
    /// <summary>
    /// Delegate for LabVIEW callback when inspection data is received from LabVIEW.
    /// </summary>
    /// <param name="jsonData">JSON string of InspectionRecord that was written to shared memory.</param>
    [ComVisible(true)]
    public delegate void InspectionDataReceivedCallback(string jsonData);

    /// <summary>
    /// Delegate for LabVIEW callback when equipment command is received from middleware.
    /// </summary>
    /// <param name="jsonCommand">JSON string of EquipmentCommand received from middleware.</param>
    [ComVisible(true)]
    public delegate void EquipmentCommandReceivedCallback(string jsonCommand);

    /// <summary>
    /// COM-visible bridge class for LabVIEW to interact with MES Middleware shared memory.
    /// Supports bidirectional communication:
    /// 1. LabVIEW → Middleware: Write inspection data, DLL monitors and callbacks when written
    /// 2. Middleware → LabVIEW: DLL monitors for commands, callbacks LabVIEW when received
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    [ProgId("MesMiddleware.LabViewBridge")]
    public class MesMiddlewareBridge : IDisposable
    {
        // Shared memory configurations
        private const string INSPECTION_SEGMENT = "MES_INSPECTION_DATA";
        private const string INSPECTION_EVENT = "MES_DATA_READY";
        private const string COMMAND_SEGMENT = "MES_EQUIPMENT_CMD";
        private const string COMMAND_EVENT = "MES_CMD_READY";
        private const string ACK_SEGMENT = "MES_EQUIPMENT_CMD_ACK";
        private const string ACK_EVENT = "MES_ACK_READY";

        // Shared memory managers
        private SharedMemoryManager _inspectionWriter;
        private SharedMemoryManager _commandReader;
        private SharedMemoryManager _ackWriter;

        // Background monitoring threads
        private Thread _inspectionMonitorThread;
        private Thread _commandMonitorThread;
        private volatile bool _isMonitoring = false;

        // LabVIEW callbacks
        private InspectionDataReceivedCallback _inspectionCallback;
        private EquipmentCommandReceivedCallback _commandCallback;

        private bool _disposed = false;
        private readonly object _lock = new object();

        /// <summary>
        /// Gets a value indicating whether the bridge is currently monitoring for events.
        /// </summary>
        public bool IsMonitoring
        {
            get { return _isMonitoring; }
        }

        /// <summary>
        /// Gets the last error message if any operation failed.
        /// </summary>
        public string LastError { get; private set; }

        /// <summary>
        /// Initializes the shared memory bridge.
        /// Must be called before any other operations.
        /// </summary>
        /// <returns>True if initialization succeeded, false otherwise.</returns>
        public bool Initialize()
        {
            lock (_lock)
            {
                try
                {
                    LastError = string.Empty;

                    // Initialize inspection data writer (LabVIEW → Middleware)
                    _inspectionWriter = new SharedMemoryManager(INSPECTION_SEGMENT, INSPECTION_EVENT);
                    _inspectionWriter.Initialize();

                    // Initialize command reader (Middleware → LabVIEW)
                    _commandReader = new SharedMemoryManager(COMMAND_SEGMENT, COMMAND_EVENT);
                    _commandReader.Initialize();

                    // Initialize acknowledgment writer (LabVIEW → Middleware)
                    _ackWriter = new SharedMemoryManager(ACK_SEGMENT, ACK_EVENT);
                    _ackWriter.Initialize();

                    return true;
                }
                catch (Exception ex)
                {
                    LastError = string.Format("Initialization failed: {0}", ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// Registers a callback for when LabVIEW writes inspection data.
        /// The callback will be invoked when the DLL detects data has been written to shared memory.
        /// </summary>
        /// <param name="callback">Callback delegate to invoke.</param>
        public void RegisterInspectionDataCallback(InspectionDataReceivedCallback callback)
        {
            lock (_lock)
            {
                _inspectionCallback = callback;
            }
        }

        /// <summary>
        /// Registers a callback for when middleware sends equipment commands.
        /// The callback will be invoked when the DLL receives a command from middleware.
        /// </summary>
        /// <param name="callback">Callback delegate to invoke.</param>
        public void RegisterEquipmentCommandCallback(EquipmentCommandReceivedCallback callback)
        {
            lock (_lock)
            {
                _commandCallback = callback;
            }
        }

        /// <summary>
        /// Starts monitoring for inspection data writes and equipment commands.
        /// Background threads will monitor shared memory and trigger callbacks.
        /// </summary>
        /// <returns>True if monitoring started successfully, false otherwise.</returns>
        public bool StartMonitoring()
        {
            lock (_lock)
            {
                if (_isMonitoring)
                {
                    LastError = "Already monitoring";
                    return false;
                }

                if (_inspectionWriter == null || _commandReader == null)
                {
                    LastError = "Not initialized. Call Initialize() first.";
                    return false;
                }

                try
                {
                    LastError = string.Empty;
                    _isMonitoring = true;

                    // Start inspection data monitoring thread (if callback registered)
                    if (_inspectionCallback != null)
                    {
                        _inspectionMonitorThread = new Thread(MonitorInspectionData);
                        _inspectionMonitorThread.IsBackground = true;
                        _inspectionMonitorThread.Start();
                    }

                    // Start command monitoring thread (if callback registered)
                    if (_commandCallback != null)
                    {
                        _commandMonitorThread = new Thread(MonitorEquipmentCommands);
                        _commandMonitorThread.IsBackground = true;
                        _commandMonitorThread.Start();
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    LastError = string.Format("Failed to start monitoring: {0}", ex.Message);
                    _isMonitoring = false;
                    return false;
                }
            }
        }

        /// <summary>
        /// Stops all monitoring threads.
        /// </summary>
        public void StopMonitoring()
        {
            lock (_lock)
            {
                _isMonitoring = false;
            }

            // Wait for threads to exit (with timeout)
            if (_inspectionMonitorThread != null && _inspectionMonitorThread.IsAlive)
            {
                _inspectionMonitorThread.Join(2000);
            }

            if (_commandMonitorThread != null && _commandMonitorThread.IsAlive)
            {
                _commandMonitorThread.Join(2000);
            }
        }

        /// <summary>
        /// Writes inspection data JSON string to shared memory (LabVIEW → Middleware).
        /// This is the primary method for LabVIEW to send inspection records.
        /// </summary>
        /// <param name="jsonData">JSON string of InspectionRecord.</param>
        /// <returns>True if write succeeded, false otherwise.</returns>
        public bool WriteInspectionData(string jsonData)
        {
            lock (_lock)
            {
                try
                {
                    if (_inspectionWriter == null)
                    {
                        LastError = "Not initialized. Call Initialize() first.";
                        return false;
                    }

                    if (string.IsNullOrEmpty(jsonData))
                    {
                        LastError = "JSON data cannot be null or empty";
                        return false;
                    }

                    LastError = string.Empty;
                    _inspectionWriter.WriteJson(jsonData);
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = string.Format("Failed to write inspection data: {0}", ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// Sends a command acknowledgment back to middleware (LabVIEW → Middleware).
        /// Call this after processing an equipment command.
        /// </summary>
        /// <param name="jsonAcknowledgment">JSON string of CommandAcknowledgment.</param>
        /// <returns>True if write succeeded, false otherwise.</returns>
        public bool WriteCommandAcknowledgment(string jsonAcknowledgment)
        {
            lock (_lock)
            {
                try
                {
                    if (_ackWriter == null)
                    {
                        LastError = "Not initialized. Call Initialize() first.";
                        return false;
                    }

                    if (string.IsNullOrEmpty(jsonAcknowledgment))
                    {
                        LastError = "JSON acknowledgment cannot be null or empty";
                        return false;
                    }

                    LastError = string.Empty;
                    _ackWriter.WriteJson(jsonAcknowledgment);
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = string.Format("Failed to write acknowledgment: {0}", ex.Message);
                    return false;
                }
            }
        }

        /// <summary>
        /// Background thread method to monitor inspection data writes.
        /// Triggers callback when LabVIEW writes data to shared memory.
        /// </summary>
        private void MonitorInspectionData()
        {
            while (_isMonitoring)
            {
                try
                {
                    // This simulates monitoring by checking if data was written
                    // In a real scenario, LabVIEW writes → signals event → this reads
                    // For now, we'll use a polling approach with small delay
                    Thread.Sleep(100);

                    // Check if there's data without blocking
                    string jsonData = _inspectionWriter.ReadJsonNoWait();
                    if (!string.IsNullOrEmpty(jsonData) && _inspectionCallback != null)
                    {
                        // Invoke LabVIEW callback
                        _inspectionCallback(jsonData);
                    }
                }
                catch
                {
                    // Continue monitoring even if read fails
                }
            }
        }

        /// <summary>
        /// Background thread method to monitor equipment commands from middleware.
        /// Triggers callback when middleware sends a command.
        /// </summary>
        private void MonitorEquipmentCommands()
        {
            while (_isMonitoring)
            {
                try
                {
                    // Wait for command signal (blocking with timeout)
                    string jsonCommand = _commandReader.ReadJson(1000); // 1 second timeout

                    if (!string.IsNullOrEmpty(jsonCommand) && _commandCallback != null)
                    {
                        // Invoke LabVIEW callback
                        _commandCallback(jsonCommand);
                    }
                }
                catch
                {
                    // Continue monitoring even if read fails
                }
            }
        }

        /// <summary>
        /// Releases all resources used by the bridge.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                StopMonitoring();

                if (_inspectionWriter != null)
                {
                    _inspectionWriter.Dispose();
                    _inspectionWriter = null;
                }

                if (_commandReader != null)
                {
                    _commandReader.Dispose();
                    _commandReader = null;
                }

                if (_ackWriter != null)
                {
                    _ackWriter.Dispose();
                    _ackWriter = null;
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer to ensure resources are released.
        /// </summary>
        ~MesMiddlewareBridge()
        {
            Dispose();
        }
    }
}
