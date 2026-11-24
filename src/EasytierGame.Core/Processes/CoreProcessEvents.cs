namespace EasytierGame.Core.Processes;

/// <summary>内核进程的输出行。</summary>
public sealed record CoreOutputLine(string Line);

/// <summary>内核进程退出。</summary>
public sealed record CoreExited(int ExitCode);
