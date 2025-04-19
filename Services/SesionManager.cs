// SessionManager.cs (create this file in the Services folder)
using LogViewerApi.Models;
using System.Collections.Concurrent;

namespace LogViewerApi.Services
{
    public class SessionManager
    {
        // Dictionary to map session IDs to extraction sessions
        private readonly ConcurrentDictionary<string, ExtractionSession> _sessions = new ConcurrentDictionary<string, ExtractionSession>();

        public void SaveSession(string sessionId, ExtractionSession session)
        {
            _sessions[sessionId] = session;
        }

        public ExtractionSession GetSession(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                return session;
            }
            return null;
        }

        public List<string> GetAllSessionIds()
        {
            return _sessions.Keys.ToList();
        }

        public void RemoveSession(string sessionId)
        {
            _sessions.TryRemove(sessionId, out _);
        }

        // Clean up old sessions
        public void CleanupOldSessions()
        {
            var keysToRemove = new List<string>();
            foreach (var kvp in _sessions)
            {
                if (kvp.Value.LastAccessTime < DateTime.Now.AddHours(-1))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _sessions.TryRemove(key, out _);
            }
        }
    }
}