using System;
using System.Text.RegularExpressions;
using Npgsql;

namespace Marten.Services
{
    public class EventStreamUnexpectedMaxEventIdException: Exception
    {
        public object Id { get; }

        public Type AggregateType { get; }

        public EventStreamUnexpectedMaxEventIdException(object id, Type aggregateType, int expected, int actual) : base($"Unexpected MAX(id) for event stream, expected {expected} but got {actual}")
        {
            Id = id;
            AggregateType = aggregateType;
        }
    }

    internal class EventStreamUnexpectedMaxEventIdExceptionTransform: IExceptionTransform
    {
        public static readonly EventStreamUnexpectedMaxEventIdExceptionTransform Instance = new EventStreamUnexpectedMaxEventIdExceptionTransform();

        private const string ExpectedMessage = "duplicate key value violates unique constraint \"pk_mt_events_stream_and_version\"";

        private const string StreamId = "<streamid>";
        private const string Version = "<version>";
        private static readonly Regex EventStreamUniqueExceptionDetailsRegex =
            new Regex(@"^Key \(stream_id, version\)=\((?<streamid>.*?), (?<version>\w+)\)");

        public bool TryTransform(Exception original, out Exception transformed)
        {
            if (!Matches(original))
            {
                transformed = null;
                return false;
            }

            var postgresException = (PostgresException) original.InnerException;

            object id = null;
            Type aggregateType = null;
            int expected = -1;
            int actual = -1;

            if (!string.IsNullOrEmpty(postgresException.Detail))
            {
                var details = EventStreamUniqueExceptionDetailsRegex.Match(postgresException.Detail);

                if (details.Groups[StreamId].Success)
                {
                    var streamId = details.Groups[StreamId].Value;

                    id = Guid.TryParse(streamId, out Guid guidStreamId) ? (object)guidStreamId : streamId;
                }

                if (details.Groups[Version].Success)
                {
                    var actualVersion = details.Groups[Version].Value;

                    if (int.TryParse(actualVersion, out int actualIntVersion))
                    {
                        actual = actualIntVersion;
                        expected = actual - 1;
                    }
                }
            }

            transformed = new EventStreamUnexpectedMaxEventIdException(id, aggregateType, expected, actual);
            return true;
        }

        private static bool Matches(Exception e)
        {
            return e?.InnerException is PostgresException pe
                   && pe.SqlState == PostgresErrorCodes.UniqueViolation
                   && pe.Message.Contains(ExpectedMessage);
        }
    }
}
