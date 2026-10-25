using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using JiuMi.ViewModels;

namespace JiuMi.Views;

/// <summary>
/// The settings page: language selection, appearance and about information.
/// </summary>
public partial class SettingsView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsView"/> class.
    /// </summary>
    public SettingsView() => InitializeComponent();

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The save picker is a platform UI boundary; a failure is reported through the info bar.")]
    private async void OnExportDiagnosticsClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        try
        {
            var options = new FilePickerSaveOptions
            {
                SuggestedFileName = viewModel.DefaultDiagnosticsFileName,
                DefaultExtension = "zip",
                FileTypeChoices =
                [
                    new FilePickerFileType("Zip")
                    {
                        Patterns = ["*.zip"],
                    },
                ],
            };

            if (viewModel.DiagnosticsDirectory is { } directory &&
                Directory.Exists(directory) &&
                await topLevel.StorageProvider.TryGetFolderFromPathAsync(directory) is { } startLocation)
            {
                options.SuggestedStartLocation = startLocation;
            }

            IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(options);
            if (file is not null)
            {
                await viewModel.ExportDiagnosticsAsync(file.Path.LocalPath);
            }
        }
        catch (Exception exception)
        {
            viewModel.ReportDiagnosticsFailure(exception);
        }
    }
}
