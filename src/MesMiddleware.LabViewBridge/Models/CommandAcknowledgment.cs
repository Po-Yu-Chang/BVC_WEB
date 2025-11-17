using System;
using System.Runtime.InteropServices;

namespace MesMiddleware.LabViewBridge.Models
{
    /// <summary>
    /// Represents equipment's acknowledgment response to a command.
    /// Sent from equipment back to middleware via shared memory.
    /// </summary>
    [ComVisible(true)]
    [Serializable]
    public class CommandAcknowledgment
    {
        /// <summary>
        /// Command ID being acknowledged (matches EquipmentCommand.CommandId).
        /// </summary>
        public Guid CommandId { get; set; }

        /// <summary>
        /// Execution status (e.g., "Success", "Failed", "Timeout", "InvalidParameter").
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Optional message providing details about execution result.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// UTC timestamp when equipment acknowledged the command.
        /// </summary>
        public DateTime AcknowledgedAt { get; set; }

        public CommandAcknowledgment()
        {
            CommandId = Guid.Empty;
            Status = string.Empty;
            Message = string.Empty;
            AcknowledgedAt = DateTime.UtcNow;
        }
    }
}
