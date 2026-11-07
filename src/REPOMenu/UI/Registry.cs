using System;
using System.Collections.Generic;
using REPOMenu.Core;

namespace REPOMenu.UI
{
    internal enum Scope
    {
        // works for anyone, in any lobby: changes only you or what you see
        Client,

        // needs you to be the host (or alone): changes the world or other players
        Host,

        // menu pages and information
        Menu
    }

    // One card in the menu: a title, a line saying what it does, maybe a switch, maybe a button,
    // maybe more controls. Cards with a switch or a button can be bound to a key.
    internal sealed class Card
    {
        public string Id;
        public string Title;
        public string Desc;
        public TabDef Tab;
        public Scope Scope;

        public Func<bool> Get;
        public Action<bool> Set;
        public Action Run;
        public string RunLabel = "Run";
        public Action Body;

        // returns null when the card can be used now, otherwise the reason it cannot
        public Func<string> Blocked;

        public bool HasSwitch => Get != null && Set != null;
        public bool Bindable => HasSwitch || Run != null;

        public bool IsOn => HasSwitch && Safe.Get(Id, Get);

        public string BlockReason()
        {
            if (Scope == Scope.Host && Game.Ready && !Game.IsHost)
            {
                return "Host only: you are a guest in someone else's lobby";
            }

            return Blocked == null ? null : Safe.Get(Id, Blocked);
        }

        public void Trigger()
        {
            var reason = BlockReason();

            if (reason != null)
            {
                Toasts.Show(Title + ": " + reason);
                return;
            }

            if (HasSwitch)
            {
                bool now = !IsOn;
                Safe.Run(Id, () => Set(now));
                Toasts.Show(Title + (now ? ": <color=#7FD1A0>on</color>" : ": <color=#E08080>off</color>"));
            }
            else if (Run != null)
            {
                Safe.Run(Id, Run);
            }
        }

        public Card WithBody(Action body)
        {
            Body = body;
            return this;
        }

        public Card When(Func<string> blocked)
        {
            Blocked = blocked;
            return this;
        }
    }

    internal sealed class TabDef
    {
        public string Name;
        public string Icon;
        public string Blurb;
        public readonly List<Card> Cards = new();

        // pages like Binds or Settings draw themselves instead of listing cards
        public Action Page;

        public Card Switch(string id, string title, string desc, Scope scope, Func<bool> get, Action<bool> set)
        {
            return Registry.Add(this, new Card { Id = id, Title = title, Desc = desc, Scope = scope, Get = get, Set = set });
        }

        public Card Action(string id, string title, string desc, Scope scope, string runLabel, Action run)
        {
            return Registry.Add(this, new Card { Id = id, Title = title, Desc = desc, Scope = scope, Run = run, RunLabel = runLabel });
        }

        public Card Panel(string id, string title, string desc, Scope scope, Action body)
        {
            return Registry.Add(this, new Card { Id = id, Title = title, Desc = desc, Scope = scope, Body = body });
        }
    }

    internal static class Registry
    {
        public static readonly List<TabDef> Tabs = new();
        public static readonly Dictionary<string, Card> ById = new();

        public static TabDef Tab(string name, string icon, string blurb)
        {
            var t = new TabDef { Name = name, Icon = icon, Blurb = blurb };
            Tabs.Add(t);
            return t;
        }

        public static Card Add(TabDef tab, Card card)
        {
            if (ById.ContainsKey(card.Id))
            {
                throw new InvalidOperationException("Two cards share the id " + card.Id);
            }

            card.Tab = tab;
            tab.Cards.Add(card);
            ById[card.Id] = card;
            return card;
        }

        // the "turn everything off" button
        public static int AllOff()
        {
            int n = 0;

            foreach (var card in ById.Values)
            {
                if (card.HasSwitch && card.IsOn)
                {
                    Safe.Run(card.Id, () => card.Set(false));
                    n++;
                }
            }

            return n;
        }
    }
}
