namespace Elara.Host.Logging
{
    public interface ILog
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
        void Metrics(string message);
    }
}
