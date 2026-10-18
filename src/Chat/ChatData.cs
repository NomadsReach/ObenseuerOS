using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ObenseuerOS
{
    internal sealed class ChatRoomDef
    {
        internal readonly string Id;
        internal readonly string Name;
        internal readonly bool Contact;
        internal readonly string[] Lines;
        internal readonly string[] Members;

        internal ChatRoomDef(string id, string name, bool contact, string[] lines, string[] members)
        {
            Id = id;
            Name = name;
            Contact = contact;
            Lines = lines;
            Members = members;
        }
    }

    internal sealed class ChatTopic
    {
        internal readonly string[] Keys;
        internal readonly string[] Replies;

        internal ChatTopic(string[] keys, string[] replies)
        {
            Keys = keys;
            Replies = replies;
        }

        internal string Match(string lower)
        {
            foreach (string key in Keys)
            {
                if (Regex.IsMatch(lower, @"\b" + Regex.Escape(key) + @"\b")) return key;
            }
            return null;
        }
    }

    internal static class ChatData
    {
        internal const int MaxLines = 100;
        internal const int MaxReturn = 30;
        internal const int MaxReturnBytes = 9 * 1024;
        internal const int MaxText = 200;
        internal const int MaxPending = 50;
        internal const int MaxHourBurst = 2;
        internal const int ReplyDelayMinutes = 2;
        internal const int ReplyJitterMinutes = 4;
        internal static readonly string[] Items = { "turnip", "potato", "milk", "cheese", "sausage", "rye bread", "sugar", "cabbage", "frozen fish" };
        internal static readonly string[] Places = { "bazaar", "mines", "metro", "tenement", "canal", "kolhola", "deekula" };
        internal static readonly ChatTopic[] Topics =
        {
            new ChatTopic(new[] { "hi", "hello", "hey", "yo" }, new[] { "Hey. Quiet in {place} today.", "Hello. Where are you right now?" }),
            new ChatTopic(new[] { "price", "prices", "cost", "worth", "sell", "selling", "cheap", "expensive" }, new[] { "Prices move with the stock. Ask at the stall.", "Check the shop, not here." }),
            new ChatTopic(Items, new[] { "{item}? Saw some near {place} this {time}.", "Still looking for {item}? Try {place}." }),
            new ChatTopic(Places, new[] { "{place} after {time}? Keep your head down.", "The {place} crowd is loud on {day}." }),
            new ChatTopic(new[] { "rain", "snow", "weather", "cold", "hot" }, new[] { "It is {weather} out. Same as always." }),
            new ChatTopic(new[] { "time", "late", "hour", "when", "now" }, new[] { "It is {time} now, if that helps." }),
        };
        internal static readonly string[] Fallback = { "No idea. Ask someone in {place}.", "Not my area. Try {place}.", "Good question. Nobody knows." };
        private static readonly string[] Quiet = new string[0];
        internal static readonly List<ChatRoomDef> Rooms = new List<ChatRoomDef>
        {
            new ChatRoomDef("internet", "# Internet", false, new[] {
                "Anyone else's {item} gone soft while it is {weather} out?",
                "{place} signal dropped again. Third time since {time}.",
                "Who sells a working {item} near {place}?",
                "The Times site is slow after {time}. Blame the cables.",
                "Does the {place} stall still open on {day}?" }, new[] { "Aleksi Kivi", "Rob Madsen", "Oscar Nilsson" }),
            new ChatRoomDef("hacking", "# hacking", false, new[] {
                "Stop posting your bank pages here, people.",
                "{place} gateway is blocking the usual ports. Use the back route.",
                "A {item} with a barcode is not a key. Ignore that guide.",
                "Nobody watches the {place} relays on {day}. Quiet night for it.",
                "Who has a clean route past the {place} gate?" }, new[] { "Ville Sköldgangster", "Aleksi Kivi" }),
            new ChatRoomDef("general", "# General", false, new[] {
                "{day} market in {place} was busy. Bring your own bag.",
                "Lost my cat near {place}, do not ask.",
                "{time} in {place}, quiet for once.",
                "Does anyone have a spare {item} to swap?",
                "It is {weather} out and nobody is going anywhere." }, new[] { "Martta Lapinjoki", "Amanda Korhonen", "Jarmo Pettinen" }),
            new ChatRoomDef("sewer", "# Open Sewer", false, new[] {
                "Opensewer mail is slow again. Use the other one.",
                "The {place} drains flood after {time}. Stay out.",
                "Found a {item} in the {place} drains. Who wants it?",
                "Watch your step on the {place} steps this {day}.",
                "Nobody tell the wardens I was here." }, new[] { "Malone Petronis", "Nina Lund", "Pate Rantanen", "Johan Wider" }),
            new ChatRoomDef("dm-42", "Amanda Korhonen", true, Quiet, new[] { "Amanda Korhonen" }),
            new ChatRoomDef("dm-25", "Rob Madsen", true, Quiet, new[] { "Rob Madsen" }),
            new ChatRoomDef("dm-24", "Oscar Nilsson", true, Quiet, new[] { "Oscar Nilsson" }),
            new ChatRoomDef("dm-2", "Mirjam Freighter", true, Quiet, new[] { "Mirjam Freighter" }),
            new ChatRoomDef("dm-37", "Malone Petronis", true, Quiet, new[] { "Malone Petronis" }),
            new ChatRoomDef("dm-23", "Omari Solheim", true, Quiet, new[] { "Omari Solheim" }),
        };

        internal static ChatRoomDef Find(string id)
        {
            foreach (ChatRoomDef def in Rooms)
            {
                if (def.Id == id) return def;
            }
            return null;
        }
    }
}
