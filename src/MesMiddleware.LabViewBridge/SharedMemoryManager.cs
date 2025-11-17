using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;

namespace MesMiddleware.LabViewBridge
{
    /// <summary>
    /// Manages shared memory read/write operations and event signaling for MES Middleware IPC.
    /// Supports both inspection data upload (equipment → middleware) and command control (middleware → equipment).
    /// </summary>
    internal class SharedMemoryManager : IDisposable
    {
        private const long DefaultSegmentSize = 10 * 1024 * 1024; // 10MB

        private readonly string _segmentName;
        private readonly string _eventName;
        private readonly long _segmentSize;

        private MemoryMappedFile _mmf;
        private EventWaitHandle _eventHandle;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the SharedMemoryManager.
        /// </summary>
        /// <param name="segmentName">Name of the shared memory segment (e.g., "MES_INSPECTION_DATA").</param>
        /// <param name="eventName">Name of the event signal (e.g., "MES_DATA_READY").</param>
        /// <param name="segmentSize">Size of the shared memory segment in bytes (default: 10MB).</param>
        public SharedMemoryManager(string segmentName, string eventName, long segmentSize = DefaultSegmentSize)
        {
            if (string.IsNullOrEmpty(segmentName))
                throw new ArgumentNullException("segmentName");
            if (string.IsNullOrEmpty(eventName))
                throw new ArgumentNullException("eventName");
            if (segmentSize <= 0)
                throw new ArgumentOutOfRangeException("segmentSize", "Segment size must be positive");

            _segmentName = segmentName;
            _eventName = eventName;
            _segmentSize = segmentSize;
        }

        /// <summary>
        /// Initializes the shared memory segment and event handle.
        /// Call this before any read/write operations.
        /// </summary>
        public void Initialize()
        {
            if (_disposed)
                throw new ObjectDisposedException("SharedMemoryManager");

            try
            {
                // Create or open shared memory segment
                _mmf = MemoryMappedFile.CreateOrOpen(_segmentName, _segmentSize, MemoryMappedFileAccess.ReadWrite);

                // Create or open event handle (auto-reset mode)
                _eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, _eventName);
            }
            catch (Exception ex)
            {
                Dispose();
                throw new InvalidOperationException(
                    string.Format("Failed to initialize shared memory segment '{0}': {1}", _segmentName, ex.Message),
                    ex);
            }
        }

        /// <summary>
        /// Writes a JSON string to shared memory and signals the event.
        /// </summary>
        /// <param name="jsonData">JSON string to write.</param>
        public void WriteJson(string jsonData)
        {
            if (_disposed)
                throw new ObjectDisposedException("SharedMemoryManager");
            if (_mmf == null || _eventHandle == null)
                throw new InvalidOperationException("SharedMemoryManager not initialized. Call Initialize() first.");
            if (string.IsNullOrEmpty(jsonData))
                throw new ArgumentNullException("jsonData");

            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonData);

            if (jsonBytes.Length + 4 > _segmentSize)
            {
                throw new ArgumentException(
                    string.Format("JSON data too large ({0} bytes). Maximum allowed: {1} bytes",
                        jsonBytes.Length, _segmentSize - 4));
            }

            try
            {
                using (var accessor = _mmf.CreateViewAccessor(0, _segmentSize, MemoryMappedFileAccess.Write))
                {
                    // Write length (4 bytes, little-endian int32)
                    accessor.Write(0, jsonBytes.Length);

                    // Write JSON data
                    accessor.WriteArray(4, jsonBytes, 0, jsonBytes.Length);
                }

                // Signal event to notify the other side
                _eventHandle.Set();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    string.Format("Failed to write to shared memory '{0}': {1}", _segmentName, ex.Message),
                    ex);
            }
        }

        /// <summary>
        /// Waits for an event signal and reads JSON data from shared memory.
        /// </summary>
        /// <param name="timeoutMilliseconds">Timeout in milliseconds to wait for signal (default: 30 seconds).</param>
        /// <returns>JSON string read from shared memory, or null if timeout occurred.</returns>
        public string ReadJson(int timeoutMilliseconds = 30000)
        {
            if (_disposed)
                throw new ObjectDisposedException("SharedMemoryManager");
            if (_mmf == null || _eventHandle == null)
                throw new InvalidOperationException("SharedMemoryManager not initialized. Call Initialize() first.");

            try
            {
                // Wait for event signal
                bool signaled = _eventHandle.WaitOne(timeoutMilliseconds);
                if (!signaled)
                {
                    return null; // Timeout
                }

                using (var accessor = _mmf.CreateViewAccessor(0, _segmentSize, MemoryMappedFileAccess.Read))
                {
                    // Read length (4 bytes)
                    int length = accessor.ReadInt32(0);

                    if (length <= 0 || length > _segmentSize - 4)
                    {
                        throw new InvalidDataException(
                            string.Format("Invalid data length in shared memory: {0} bytes", length));
                    }

                    // Read JSON data
                    byte[] buffer = new byte[length];
                    accessor.ReadArray(4, buffer, 0, length);

                    return Encoding.UTF8.GetString(buffer);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    string.Format("Failed to read from shared memory '{0}': {1}", _segmentName, ex.Message),
                    ex);
            }
        }

        /// <summary>
        /// Reads JSON data from shared memory without waiting for an event signal.
        /// Use this for polling scenarios.
        /// </summary>
        /// <returns>JSON string read from shared memory, or null if no data available.</returns>
        public string ReadJsonNoWait()
        {
            if (_disposed)
                throw new ObjectDisposedException("SharedMemoryManager");
            if (_mmf == null)
                throw new InvalidOperationException("SharedMemoryManager not initialized. Call Initialize() first.");

            try
            {
                using (var accessor = _mmf.CreateViewAccessor(0, _segmentSize, MemoryMappedFileAccess.Read))
                {
                    // Read length (4 bytes)
                    int length = accessor.ReadInt32(0);

                    if (length <= 0 || length > _segmentSize - 4)
                    {
                        return null; // No valid data
                    }

                    // Read JSON data
                    byte[] buffer = new byte[length];
                    accessor.ReadArray(4, buffer, 0, length);

                    return Encoding.UTF8.GetString(buffer);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Releases all resources used by the SharedMemoryManager.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_mmf != null)
                {
                    _mmf.Dispose();
                    _mmf = null;
                }

                if (_eventHandle != null)
                {
                    _eventHandle.Dispose();
                    _eventHandle = null;
                }

                _disposed = true;
            }
        }
    }
}
