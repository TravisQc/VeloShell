using System.Collections.Generic;
using System.Threading.Tasks;
using VeloShell.Models;
using VeloShell.ViewModels;

namespace VeloShell.Services;

public sealed record MasterPasswordOutcome(bool Succeeded, bool ResetRequested);

public sealed record ConnectionEditOutcome(Connection Connection, string? Secret, bool SecretProvided);

public sealed record FolderChoice(string Id, string Path);

/// <summary>Shows modal dialogs on behalf of view models, keeping window concerns out of them.</summary>
public interface IDialogService
{
    Task<MasterPasswordOutcome> MasterPasswordAsync(MasterPasswordMode mode);
    Task<ConnectionEditOutcome?> EditConnectionAsync(Connection? existing, bool secretAlreadyStored);
    Task<bool> ConfirmAsync(string title, string message);
    Task<string?> PromptAsync(string title, string? initial = null);
    Task<string?> PickFolderAsync(IReadOnlyList<FolderChoice> folders);
}
