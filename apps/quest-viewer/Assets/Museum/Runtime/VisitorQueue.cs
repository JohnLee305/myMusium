using System;
using System.Collections.Generic;

namespace FamilyMuseum
{
    // One instance per room/exhibit viewing place. Called by the simulation thread.
    // Release only AFTER the owner has physically cleared the place, not when viewing ends.
    public sealed class VisitorQueue
    {
        sealed class Entry
        {
            public string Guest;
            public long Ticket;
        }
        readonly LinkedList<Entry> waiting = new LinkedList<Entry>();
        readonly Dictionary<string, LinkedListNode<Entry>> entries =
            new Dictionary<string, LinkedListNode<Entry>>(StringComparer.Ordinal);
        Entry owner;
        long nextTicket;

        public string Owner => owner == null ? null : owner.Guest;
        public int WaitingCount => waiting.Count;
        public int WaitingRank(string guest)
        {
            int rank = 0;
            for (var node = waiting.First; node != null; node = node.Next, rank++)
                if (node.Value.Guest == guest) return rank;
            return -1;
        }
        public bool Contains(string guest) => !string.IsNullOrEmpty(guest) &&
            ((owner != null && owner.Guest == guest) || entries.ContainsKey(guest));

        public long Join(string guest)
        {
            if (string.IsNullOrEmpty(guest)) throw new ArgumentException("Guest ID is required.", nameof(guest));
            if (owner != null && owner.Guest == guest) return owner.Ticket;
            if (entries.TryGetValue(guest, out var existing)) return existing.Value.Ticket;
            var entry = new Entry { Guest = guest, Ticket = checked(++nextTicket) };
            entries.Add(guest, waiting.AddLast(entry));
            return entry.Ticket;
        }

        public bool TryAcquire(string guest)
        {
            if (string.IsNullOrEmpty(guest)) return false;
            if (owner != null) return owner.Guest == guest;
            if (waiting.First == null || waiting.First.Value.Guest != guest) return false;
            owner = waiting.First.Value;
            waiting.RemoveFirst();
            entries.Remove(guest);
            return true;
        }

        public bool Release(string guest)
        {
            if (owner == null || owner.Guest != guest) return false;
            owner = null;
            return true;
        }

        // For a guest actually removed from the simulation. Repath/camera changes must not cancel.
        public bool Cancel(string guest)
        {
            if (string.IsNullOrEmpty(guest)) return false;
            if (Release(guest)) return true;
            if (!entries.TryGetValue(guest, out var node)) return false;
            waiting.Remove(node);
            entries.Remove(guest);
            return true;
        }

        public void Clear()
        {
            owner = null;
            waiting.Clear();
            entries.Clear();
            // Do not reuse tickets when the queue is cleared.
        }
    }
}
