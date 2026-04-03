using Avalonia.Controls;
using Azrng.Core.Model;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Views.Database.Workbench;

public partial class DatabaseTypeSelector : UserControl
{
    public DatabaseTypeSelector()
    {
        InitializeComponent();
        SetupCardClickHandlers();
    }

    private void SetupCardClickHandlers()
    {
        RegisterCardTap("SqlCard", DatabaseType.SqlServer);
        RegisterCardTap("MySqlCard", DatabaseType.MySql);
        RegisterCardTap("PostgreCard", DatabaseType.PostgresSql);
        RegisterCardTap("SqliteCard", DatabaseType.Sqlite);
        RegisterCardTap("OracleCard", DatabaseType.Oracle);
        RegisterCardTap("DamengCard", DatabaseType.Dm);
    }

    private void RegisterCardTap(string controlName, DatabaseType dbType)
    {
        if (this.FindControl<Control>(controlName) is { } card)
        {
            card.Tapped += (_, _) => OnCardClicked(dbType);
        }
    }

    private void OnCardClicked(DatabaseType dbType)
    {
        if (DataContext is ConnectionDialogViewModel viewModel)
        {
            viewModel.SelectDatabaseTypeCommand?.Execute(dbType);
        }
    }
}
