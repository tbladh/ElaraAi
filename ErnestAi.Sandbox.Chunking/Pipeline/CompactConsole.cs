using System;

namespace ErnestAi.Sandbox.Chunking;

/// <summary>
/// Console helper to compact consecutive silence into dots on a single line
/// and ensure state/speech lines begin on a fresh line.
/// </summary>
public sealed class CompactConsole
{
    private readonly object _lock = new();
    private bool _inSilenceRun;

    public void WriteSilenceDot()
    {
        lock (_lock)
        {
            Console.Write(".");
            _inSilenceRun = true;
        }
    }

    public void WriteSpeechLine(string message)
    {
        lock (_lock)
        {
            if (_inSilenceRun)
            {
                Console.WriteLine();
                _inSilenceRun = false;
            }
            Console.WriteLine(message);
        }
    }

    public void WriteStateLine(string message)
    {
        // Treat state like speech: force new line first
        WriteSpeechLine(message);
    }

    public void FlushSilence()
    {
        lock (_lock)
        {
            if (_inSilenceRun)
            {
                Console.WriteLine();
                _inSilenceRun = false;
            }
        }
    }
}
