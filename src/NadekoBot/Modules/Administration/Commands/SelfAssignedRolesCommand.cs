using Discord;
using Discord.Commands;
using Discord.Net;
using NadekoBot.Attributes;
using NadekoBot.Services;
using NadekoBot.Services.Database;
using NadekoBot.Services.Database.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class SelfAssignedRolesCommands
        {
            private ILocalization _l;

            public SelfAssignedRolesCommands()
            {
                _l = NadekoBot.Localizer;
            }

            [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageRoles)]
            public async Task Asar(IUserMessage umsg, [Remainder] IRole role)
            {
                var channel = (ITextChannel)umsg.Channel;

                IEnumerable<SelfAssignedRole> roles;

                string msg;
                using (var uow = DbHandler.UnitOfWork())
                {
                    roles = uow.SelfAssignedRoles.GetFromGuild(channel.Guild.Id);
                    if (roles.Any(s => s.RoleId == role.Id && s.GuildId == role.GuildId))
                    {
                        msg = string.Format(_l["administration_asar_error_already_list", channel.Guild.Id], role.Name);
                    }
                    else
                    {
                        uow.SelfAssignedRoles.Add(new SelfAssignedRole {
                            RoleId = role.Id,
                            GuildId = role.GuildId
                        });
                        await uow.CompleteAsync();
                        msg = string.Format(_l["administration_asar_success", channel.Guild.Id], role.Name);
                    }
                }
                await channel.SendMessageAsync(msg).ConfigureAwait(false);
            }

            [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageRoles)]
            public async Task Rsar(IUserMessage umsg, [Remainder] IRole role)
            {
                var channel = (ITextChannel)umsg.Channel;

                bool success;
                using (var uow = DbHandler.UnitOfWork())
                {
                    success = uow.SelfAssignedRoles.DeleteByGuildAndRoleId(role.GuildId, role.Id);
                    await uow.CompleteAsync();
                }
                if (!success)
                {
                    await channel.SendMessageAsync(_l["administration_rsar_error_not_assignable", channel.Guild.Id]).ConfigureAwait(false);
                    return;
                }
                await channel.SendMessageAsync(string.Format(_l["administration_rsar_success", channel.Guild.Id], role.Name)).ConfigureAwait(false);
            }

            [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
            [RequireContext(ContextType.Guild)]
            public async Task Lsar(IUserMessage umsg)
            {
                var channel = (ITextChannel)umsg.Channel;

                var toRemove = new HashSet<SelfAssignedRole>();
                var removeMsg = new StringBuilder();
                var msg = new StringBuilder();
                using (var uow = DbHandler.UnitOfWork())
                {
                    var roleModels = uow.SelfAssignedRoles.GetFromGuild(channel.Guild.Id);
                    msg.AppendLine(string.Format(_l["administration_lsar_number", channel.Guild.Id], roleModels.Count()));
                    
                    foreach (var roleModel in roleModels)
                    {
                        var role = channel.Guild.Roles.FirstOrDefault(r => r.Id == roleModel.RoleId);
                        if (role == null)
                        {
                            uow.SelfAssignedRoles.Remove(roleModel);
                        }
                        else
                        {
                            msg.Append($"**{role.Name}**, ");
                        }
                    }
                    foreach (var role in toRemove)
                    {
                        removeMsg.AppendLine(string.Format(_l["administration_lsar_cleanup", channel.Guild.Id], role.Id));
                    }
                    await uow.CompleteAsync();
                }
                await channel.SendMessageAsync(msg.ToString() + "\n\n" + removeMsg.ToString()).ConfigureAwait(false);
            }

            [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageRoles)]
            public async Task Tesar(IUserMessage umsg)
            {
                var channel = (ITextChannel)umsg.Channel;

                bool areExclusive;
                using (var uow = DbHandler.UnitOfWork())
                {
                    var config = uow.GuildConfigs.For(channel.Guild.Id);

                    areExclusive = config.ExclusiveSelfAssignedRoles = !config.ExclusiveSelfAssignedRoles;
                    await uow.CompleteAsync();
                }
                await channel.SendMessageAsync(areExclusive ? _l["administration_tesar_exclusive", channel.Guild.Id] :
                    _l["administration_tesar_not_exclusive", channel.Guild.Id]);
            }

            [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
            [RequireContext(ContextType.Guild)]
            public async Task Iam(IUserMessage umsg, [Remainder] IRole role)
            {
                var channel = (ITextChannel)umsg.Channel;
                var guildUser = (IGuildUser)umsg.Author;
                var usrMsg = (IUserMessage)umsg;

                GuildConfig conf;
                IEnumerable<SelfAssignedRole> roles;
                using (var uow = DbHandler.UnitOfWork())
                {
                    conf = uow.GuildConfigs.For(channel.Guild.Id);
                    roles = uow.SelfAssignedRoles.GetFromGuild(channel.Guild.Id);
                }
                SelfAssignedRole roleModel;
                if ((roleModel = roles.FirstOrDefault(r=>r.RoleId == role.Id)) == null)
                {
                    await channel.SendMessageAsync(_l["administration_iam_not_assignable", channel.Guild.Id]).ConfigureAwait(false);
                    return;
                }
                if (guildUser.Roles.Contains(role))
                {
                    await channel.SendMessageAsync(string.Format(_l["administration_iam_already_have_role", channel.Guild.Id], role.Name)).ConfigureAwait(false);
                    return;
                }

                if (conf.ExclusiveSelfAssignedRoles)
                {
                    var sameRoles = guildUser.Roles.Where(r => roles.Any(rm => rm.RoleId == r.Id));
                    if (sameRoles.Any())
                    {
                        await channel.SendMessageAsync(string.Format(_l["administration_iam_have_exclusive", channel.Guild.Id], sameRoles.FirstOrDefault().Name)).ConfigureAwait(false);
                        return;
                    }
                    
                }
                try
                {
                    await guildUser.AddRolesAsync(role).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    await channel.SendMessageAsync(_l["administration_iam_insufficient_permission", channel.Guild.Id]).ConfigureAwait(false);
                    return;
                }
                var msg = await channel.SendMessageAsync(string.Format(_l["administration_iam_success", channel.Guild.Id], role.Name)).ConfigureAwait(false);

                if (conf.AutoDeleteSelfAssignedRoleMessages)
                {
                    var t = Task.Run(async () =>
                    {
                        await Task.Delay(3000).ConfigureAwait(false);
                        try { await msg.DeleteAsync().ConfigureAwait(false); } catch { } // if 502 or something, i don't want bot crashing
                        try { await usrMsg.DeleteAsync().ConfigureAwait(false); } catch { }
                    });
                }
            }

            [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
            [RequireContext(ContextType.Guild)]
            public async Task Iamnot(IUserMessage umsg, [Remainder] IRole role)
            {
                var channel = (ITextChannel)umsg.Channel;
                var guildUser = (IGuildUser)umsg.Author;

                GuildConfig conf;
                IEnumerable<SelfAssignedRole> roles;
                using (var uow = DbHandler.UnitOfWork())
                {
                    conf = uow.GuildConfigs.For(channel.Guild.Id);
                    roles = uow.SelfAssignedRoles.GetFromGuild(channel.Guild.Id);
                }
                SelfAssignedRole roleModel;
                if ((roleModel = roles.FirstOrDefault(r => r.RoleId == role.Id)) == null)
                {
                    await channel.SendMessageAsync(_l["administration_iamnot_not_assignable", channel.Guild.Id]).ConfigureAwait(false);
                    return;
                }
                if (!guildUser.Roles.Contains(role))
                {
                    await channel.SendMessageAsync(string.Format(_l["administration_iamnot_have_not_role", channel.Guild.Id], role.Name)).ConfigureAwait(false);
                    return;
                }
                try
                {
                    await guildUser.RemoveRolesAsync(role).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    await channel.SendMessageAsync(_l["administration_iamnot_insufficient_permission", channel.Guild.Id]).ConfigureAwait(false);
                    return;
                }
                var msg = await channel.SendMessageAsync(string.Format(_l["administration_iamnot_success", channel.Guild.Id], role.Name)).ConfigureAwait(false);

                if (conf.AutoDeleteSelfAssignedRoleMessages)
                {
                    var t = Task.Run(async () =>
                    {
                        await Task.Delay(3000).ConfigureAwait(false);
                        try { await msg.DeleteAsync().ConfigureAwait(false); } catch { } // if 502 or something, i don't want bot crashing
                        try { await umsg.DeleteAsync().ConfigureAwait(false); } catch { }
                    });
                }
            }
        }
    }
}