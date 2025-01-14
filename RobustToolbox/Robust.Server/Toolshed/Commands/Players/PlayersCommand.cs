﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Players;
using Robust.Shared.Toolshed;
using Robust.Shared.Toolshed.Errors;
using Robust.Shared.Utility;

namespace Robust.Server.Toolshed.Commands.Players;

[ToolshedCommand]
public sealed class PlayerCommand : ToolshedCommand
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    [CommandImplementation("list")]
    public IEnumerable<IPlayerSession> Players()
        => _playerManager.ServerSessions;

    [CommandImplementation("self")]
    public IPlayerSession Self([CommandInvocationContext] IInvocationContext ctx)
    {
        if (ctx.Session is null)
        {
            ctx.ReportError(new NotForServerConsoleError());
        }

        return (IPlayerSession)ctx.Session!;
    }

    [CommandImplementation("imm")]
    public ICommonSession Immediate(
        [CommandInvocationContext] IInvocationContext ctx,
        [CommandArgument] string username
        )
    {
        _playerManager.TryGetSessionByUsername(username, out var session);

        if (session is null)
        {
            if (Guid.TryParse(username, out var guid))
            {
                _playerManager.TryGetSessionById(new NetUserId(guid), out session);
            }
        }

        if (session is null)
        {
            ctx.ReportError(new NoSuchPlayerError(username));
        }

        return session!;
    }

    [CommandImplementation("entity")]
    public IEnumerable<EntityUid> GetPlayerEntity([PipedArgument] IEnumerable<IPlayerSession> sessions)
    {
        return sessions.Select(x => x.AttachedEntity).Where(x => x is not null).Cast<EntityUid>();
    }

    [CommandImplementation("entity")]
    public EntityUid GetPlayerEntity([PipedArgument] IPlayerSession sessions)
    {
        return sessions.AttachedEntity ?? default;
    }
}

public record struct NoSuchPlayerError(string Username) : IConError
{
    public FormattedMessage DescribeInner()
    {
        return FormattedMessage.FromMarkup($"No player with the username/GUID {Username} could be found.");
    }

    public string? Expression { get; set; }
    public Vector2i? IssueSpan { get; set; }
    public StackTrace? Trace { get; set; }
}
