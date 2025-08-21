using System.Text;

namespace Elara.Updater.Dev
{
    public class UpdaterState
    {
        public bool IsBusy => _busyCount > 0;
        public DateTimeOffset? LastActionAt { get; private set; }
        public string LastAction { get; private set; } = "Idle";
        public string LastResult { get; private set; } = string.Empty;

        private int _busyCount = 0;
        private readonly object _lock = new();

        public IDisposable BeginAction(string action)
        {
            lock (_lock)
            {
                _busyCount++;
                LastAction = action;
                LastActionAt = DateTimeOffset.Now;
            }
            return new Ender(this);
        }

        private void End(string? result)
        {
            lock (_lock)
            {
                if (_busyCount > 0) _busyCount--;
                if (!string.IsNullOrWhiteSpace(result)) LastResult = result!;
            }
        }

        private class Ender : IDisposable
        {
            private readonly UpdaterState _state;
            private bool _disposed;
            public Ender(UpdaterState state) { _state = state; }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _state.End(null);
            }
        }
    }
}
