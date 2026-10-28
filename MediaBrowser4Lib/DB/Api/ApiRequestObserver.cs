using System;
using System.Threading;

namespace MediaBrowser4.DB.Api
{
    public sealed class ApiRequestInfo
    {
        public string Method { get; set; }
        public string Path { get; set; }

        public string Description
        {
            get { return (Method ?? String.Empty) + " " + (Path ?? String.Empty); }
        }
    }

    public interface IApiRequestObserver
    {
        T Execute<T>(ApiRequestInfo request, Func<CancellationToken, T> operation);
    }

    public static class ApiRequestObserverHost
    {
        public static IApiRequestObserver Observer { get; set; }
    }
}
