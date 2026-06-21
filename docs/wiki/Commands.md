# Commands

exlib owns a single shared command root for the whole family: **`/exmod`** on the server and
**`.exmod`** on the client. Every mod's sub-commands hang off it, so players learn one command.
This page covers the built-in sub-commands and how to add your own.

## The `exmod` root

`ExmodCommand` (`[CommandRegister(Side = Universal)]`) registers the root on both sides. On its own
it just prints help; the useful behaviour lives in sub-commands. exlib ships these:

| Command | Side | What it does |
| --- | --- | --- |
| `/exmod config [<mod> [<value> [<new>]]]` | server | Edit any [manageable config](Config-System) value live. |
| `/exmod recipes [<mod> [<level>]]` | server | Switch a mod's [recipe-cost level](Recipe-Costs). |
| `/exmod heal` | server | Sweep loaded chunks and recreate orphaned block entities ([healing](Migrations-and-Healing)). |
| `.exmod network hi` / `.exmod network unhi` | client | Toggle the transparent per-network colour highlight ([block networks](Block-Networks)). |

`/exmod config` and `/exmod recipes` are mod-agnostic: any mod that registers a manageable config
or a recipe profile shows up automatically, no per-mod command code.

## Adding your own sub-command

Implement `IExSubCommand`, tag it with the side, and point `ParentName` at `"exmod"`:

```csharp
[SubCommandRegister(Side = EnumAppSide.Server)]
public sealed class StatusSubCommand : IExSubCommand
{
    public string ParentName => "exmod";

    public void Register(ICoreAPI api, Mod mod, IChatCommand parent)
    {
        parent.BeginSubCommand("status")
              .WithDescription(Lang.Get(mod.Info.ModID + ":command-status-desc"))
              .RequiresPrivilege(Privilege.controlserver)
              .HandleWith(args =>
              {
                  // ...
                  return TextCommandResult.Success("ok");
              })
              .EndSubCommand();
    }
}
```

A client-side sub-command casts `api` to `ICoreClientAPI` and is gated with
`[SubCommandRegister(Side = EnumAppSide.Client)]`. The registry creates the `exmod` parent if it
doesn't exist yet, so order between mods doesn't matter.

Register your sub-commands with `CommandRegistry.RegisterAll(api, Mod, GetType().Assembly)` - see
**[Registries](Registries)**.

## VTML pitfall in command output

Chat output is rendered as **VTML**. A literal `<` opens a tag and silently eats the rest of the
message, so a usage string like `Usage: /exmod config <mod>` truncates at `<mod>`. Use square
brackets instead - `Usage: /exmod config [mod]` - in any command-result or usage text. (Vanilla's
`<hk>` is a hotkey-code tag and renders `?` for command strings, so don't use it to wrap commands;
prefer `<strong>`.)

## Related pages

- [Registries](Registries) - `IExCommand` / `IExSubCommand` and `CommandRegistry`.
- [Config System](Config-System) - what `/exmod config` edits.
- [Recipe Costs](Recipe-Costs) - what `/exmod recipes` switches.
