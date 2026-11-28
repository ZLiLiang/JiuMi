namespace JiuMi.Application.MyJiuMi;

/// <summary>
/// Reads the current system pointer location in physical screen pixels for desktop sit-watch tracking.
/// </summary>
public interface IMyJiuMiCursorPositionProvider
{
    /// <summary>
    /// Attempts to read the current system cursor position in physical screen pixels.
    /// </summary>
    /// <param name="position">The cursor coordinates in physical screen pixels when available.</param>
    /// <returns><see langword="true"/> when the cursor position was read.</returns>
    bool TryGetPosition(out MyJiuMiScreenPoint position);
}
