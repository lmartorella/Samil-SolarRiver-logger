

using Lucky.Home.Services;
using Lucky.Home.Sinks;
/**
 * Fakes to make the old code to still compile as a seperate lib
 * See https://github.com/lmartorella/net/tree/1.5 for old implementations
 */

namespace Lucky.Db
{
    public class TimeSample
    {
        public DateTime TimeStamp;
        public DateTime FromInvariantTime(DateTime t)
        {
            return t;
        }
    }

    public abstract class DayTimeSample<T>
    {
        public DateTime Date;
        public abstract bool Aggregate(DateTime date, IEnumerable<T> data);
        public DateTime FromInvariantTime(TimeSpan t)
        {
            return Date + t;
        }
    }

    public interface ITimeSeries<T, T1>
    {
        void AddNewSample(T data);
        T1 GetAggregatedData();
        T GetLastSample();
    }
}

namespace Lucky.Home
{
    public class CsvAttribute : Attribute
    {
        public CsvAttribute(string _fmt)
        {

        }
    }

    public interface IDevice
    {

    }

    public class DeviceBase : IDevice
    {
        public ILogger Logger;
        public OnlineStatus OnlineStatus;
        protected virtual async Task OnTerminate()
        {

        }
        public SinkBase[] Sinks;
    }

    public enum OnlineStatus
    {
        Online
    }

    public class WebResponse
    {

    }

    public interface IService
    {
        
    }

    public interface ILogger
    {
        void Exception(Exception exc);
        void Log(string v);
        void Log(string v1, string v2, object v3);
    }

    public class Logger : ILogger
    {
        public void Exception(Exception exc)
        {
            Console.Error.WriteLine("EXC " + exc);
        }

        public void Log(string key)
        {
            Console.Write(key);
        }

        public void Log(string key, string k1, object v1)
        {
            Console.Write(key + ", " + k1 + ": " + v1);
        }
    }

    public interface ILoggerFactory : IService
    {
        ILogger Create(string name);
    }

    public class LoggerFactory : ILoggerFactory
    {
        public ILogger Create(string name)
        {
            return new Logger();
        }
    }

    public interface IStatusUpdate
    {
        string Text { get; set; }
        DateTime TimeStamp { get; }

        bool Update(Action value);
    }

    public interface INotificationService : IService
    {
        IStatusUpdate EnqueueStatusUpdate(string v1, string v2);
        void SendMail(string title, object body, bool v);
    }

    public class NotificationService : INotificationService
    {
        public class StatusUpdate : IStatusUpdate
        {
            public string Text { get; set; }
            public DateTime TimeStamp { get; set; }
            public bool Update(Action callback)
            {
                callback();
                return true;
            }
        }

        public IStatusUpdate EnqueueStatusUpdate(string v1, string v2)
        {
            return new StatusUpdate();
        }

        public void SendMail(string title, object body, bool v)
        {
            Console.WriteLine("Mail sent");
        }
    }
}

namespace Lucky.Home.Services
{
    public class PipeServer : IService
    {
        public class Request
        {
            public string Command;
        }

        public class MessageEventArgs
        {
            public Request Request;
            public object Response;
        }

        public EventHandler<MessageEventArgs> Message;
    }
}

namespace Lucky.Home.Sinks
{
    public class SinkBase
    {
        public bool IsOnline;
    }
}

namespace Lucky.Home
{
    public class Manager
    {
        public static T GetService<T>() where T : class, IService
        {
            if (typeof(T) == typeof(ILoggerFactory))
            {
                return (T)(object)new LoggerFactory();
            }
            if (typeof(T) == typeof(PipeServer))
            {
                return (T)(object)new PipeServer();
            }
            if (typeof(T) == typeof(INotificationService))
            {
                return (T)(object)new NotificationService();
            }
            return null;
        }
    }

    public class DeviceAttribute : Attribute
    {
        public DeviceAttribute(string _name)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RequiresAttribute : Attribute
    {
        public RequiresAttribute(Type _name)
        {
        }

        public bool Optional { get; set; }
    }

    public class HalfDuplexLineSink : SinkBase
    {
        internal Task<Tuple<byte[], Error>> SendReceive(byte[] bytes, bool v1, bool v2, string v3)
        {
            throw new NotImplementedException();
        }

        public enum Error
        {
            Ok
        }
    }

    public class AnalogIntegratorSink : SinkBase
    {
        internal Task<double> ReadData(double v)
        {
            throw new NotImplementedException();
        }
    }

    public class MockCommandSink : SinkBase
    {
        internal Task<string> ReadCommand()
        {
            throw new NotImplementedException();
        }

        internal Task WriteResponse(string resp)
        {
            throw new NotImplementedException();
        }
    }
}
