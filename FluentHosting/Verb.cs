using System;

namespace FluentHosting
{
	[Flags]
    public enum Verb
    {
        None = 0,
        Get = 1,
        Put = 2,
        Post = 4,
        Delete = 8,
        Options = 16,
        All = Get | Put | Post | Delete | Options
    }
}
