using System;
using System.IO;

namespace LuxBurn
{
    internal sealed class CancellationTokenSource : IDisposable
    {
        private readonly CancellationTokenState _state = new CancellationTokenState();

        public CancellationToken Token
        {
            get { return new CancellationToken(_state); }
        }

        public void Cancel()
        {
            _state.IsCancellationRequested = true;
        }

        public void Dispose()
        {
        }
    }

    internal struct CancellationToken
    {
        private readonly CancellationTokenState _state;

        public static readonly CancellationToken None = new CancellationToken(new CancellationTokenState());

        internal CancellationToken(CancellationTokenState state)
        {
            _state = state;
        }

        public bool IsCancellationRequested
        {
            get { return _state != null && _state.IsCancellationRequested; }
        }

        public void ThrowIfCancellationRequested()
        {
            if (IsCancellationRequested)
                throw new OperationCanceledException();
        }
    }

    internal sealed class CancellationTokenState
    {
        public volatile bool IsCancellationRequested;
    }

    internal static class LegacyPaths
    {
        public static string Combine(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
                return string.Empty;

            string result = parts[0] ?? string.Empty;
            for (int i = 1; i < parts.Length; i++)
                result = Path.Combine(result, parts[i] ?? string.Empty);

            return result;
        }

        public static string ProgramFilesX86()
        {
            string path = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (string.IsNullOrEmpty(path))
                path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            return path;
        }
    }
}
