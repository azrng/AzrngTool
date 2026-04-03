using System;
using Avalonia.Controls;
using Avalonia.Input;
using Azrng.Core.Model;
using AzrngTools.Models.Database;
using AzrngTools.ViewModels.Database;

namespace AzrngTools.Views.Database.Workbench;

public partial class DatabaseTypeSelector : UserControl
{
    public DatabaseTypeSelector()
    {
        InitializeComponent();
        SetupCardClickHandlers();
    }

    /// <summary>
    /// 设置卡片点击事件处理器
    /// </summary>
    private void SetupCardClickHandlers()
    {
        // 为每个数据库卡片添加 Tapped 事件处理
        if (this.FindControl<Border>("SqlCard") is Border sqlCard)
        {
            sqlCard.Tapped += (s, e) => OnCardClicked(DatabaseType.SqlServer);
        }

        if (this.FindControl<Border>("MySqlCard") is Border mySqlCard)
        {
            mySqlCard.Tapped += (s, e) => OnCardClicked(DatabaseType.MySql);
        }

        if (this.FindControl<Border>("PostgreCard") is Border postgreCard)
        {
            postgreCard.Tapped += (s, e) => OnCardClicked(DatabaseType.PostgresSql);
        }

        if (this.FindControl<Border>("SqliteCard") is Border sqliteCard)
        {
            sqliteCard.Tapped += (s, e) => OnCardClicked(DatabaseType.Sqlite);
        }

        if (this.FindControl<Border>("OracleCard") is Border oracleCard)
        {
            oracleCard.Tapped += (s, e) => OnCardClicked(DatabaseType.Oracle);
        }

        if (this.FindControl<Border>("DamengCard") is Border damengCard)
        {
            damengCard.Tapped += (s, e) => OnCardClicked(DatabaseType.Dm);
        }
    }

    /// <summary>
    /// 卡片点击处理
    /// </summary>
    private void OnCardClicked(DatabaseType dbType)
    {
        // 通过 DataContext 调用 ViewModel 的命令
        if (DataContext is ConnectionDialogViewModel viewModel)
        {
            viewModel.SelectDatabaseTypeCommand?.Execute(dbType);
        }
    }
}
