namespace YtMusicController.Core.Player;

public sealed class PlayerNotReadyException(string message = "The YouTube Music player is not ready.") : InvalidOperationException(message);
