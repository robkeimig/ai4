namespace AI;

/// <summary>
/// This determines specific modes of interaction with a given IOBuffer.
/// </summary>
public enum IOBufferRole
{
    /// <summary>
    /// Reads a single byte value from the current cursor.
    /// </summary>
    CursorRead,

    /// <summary>
    /// Sets a single byte value at the current cursor.
    /// </summary>
    CursorWrite,

    /// <summary>
    /// This neuron is activated by the buffer using a tick differential spike pair encoding the value.
    /// </summary>
    CursorReadOutput,

    /// <summary>
    /// This neuron is sampled by the buffer using the spike history collection to determine the appropriate value to write (tick differential last 2 spikes % 255).
    /// </summary>
    CursorWriteInput,

    /// <summary>
    /// Advances the cursor forward 1 byte.
    /// If this exceeds the bounds, CursorMaxLimitNotifier is raised.
    /// </summary>
    CursorIncrementer,

    /// <summary>
    /// Decrements the cursor forward 1 byte.
    /// If this exceeds the bounds, CursorMinLimitNotifier is raised.
    /// </summary>
    CursorDecrementer,

    /// <summary>
    /// Raised when the cursor exceeds limits
    /// </summary>
    CursorMaxLimitNotifier,
    CursorMinLimitNotifier,
}
