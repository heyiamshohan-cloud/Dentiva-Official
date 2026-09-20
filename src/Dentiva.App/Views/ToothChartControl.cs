using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dentiva.App.Services;
using Dentiva.Core.Domain;
using Dentiva.Core.Teeth;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Views;

/// <summary>
/// Professional odontogram: FDI permanent teeth with per-tooth condition
/// coloring, tooth notes and one-click updates. Deciduous teeth are shown
/// when the patient has any milk-tooth record.
/// </summary>
public sealed class ToothChartControl : UserControl
{
    private readonly long _patientId;
    private readonly VisitRepository _visits;
    private IReadOnlyList<ToothRecord> _records;

    private static readonly Dictionary<ToothCondition, Brush> Fill = new()
    {
        [ToothCondition.Healthy] = new SolidColorBrush(Color.FromRgb(0xF3, 0xF7, 0xFA)),
        [ToothCondition.Decayed] = new SolidColorBrush(Color.FromRgb(0xFB, 0xED, 0xEA)),
        [ToothCondition.Filled] = new SolidColorBrush(Color.FromRgb(0xEA, 0xF1, 0xF8)),
        [ToothCondition.RootCanalTreated] = new SolidColorBrush(Color.FromRgb(0xE7, 0xF4, 0xF8)),
        [ToothCondition.Crowned] = new SolidColorBrush(Color.FromRgb(0xFB, 0xF2, 0xE1)),
        [ToothCondition.Extracted] = new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xF6)),
        [ToothCondition.Missing] = new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xF6)),
        [ToothCondition.Implanted] = new SolidColorBrush(Color.FromRgb(0xE6, 0xF5, 0xEE)),
        [ToothCondition.Other] = new SolidColorBrush(Color.FromRgb(0xF3, 0xF7, 0xFA)),
    };

    public ToothChartControl(IReadOnlyList<ToothRecord> records, long patientId)
    {
        _records = records;
        _patientId = patientId;
        _visits = AppServices.Get<VisitRepository>();

        var showDeciduous = records.Any(r => r.Dentition == Dentition.Deciduous);
        BuildLayout(showDeciduous);
    }

    private void BuildLayout(bool deciduous)
    {
        var root = new StackPanel { Orientation = Orientation.Vertical };

        root.Children.Add(BuildLegend());
        root.Children.Add(BuildArchRow(ToothCatalog.UpperArch(deciduous), "Upper"));
        root.Children.Add(BuildArchRow(ToothCatalog.LowerArch(deciduous), "Lower"));

        var toggle = new Button
        {
            Content = deciduous ? "Show permanent teeth" : "Show milk teeth",
            Style = (Style)Application.Current.Resources["GhostButton"],
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 10, 0, 0),
            FontSize = 11.5,
        };
        toggle.Click += (_, _) =>
        {
            BuildLayout(!deciduous);
            Content = root;
        };

        var panel = new StackPanel { Orientation = Orientation.Vertical };
        panel.Children.Add(root);
        panel.Children.Add(toggle);
        Content = panel;
    }

    private UIElement BuildLegend()
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        foreach (var condition in new[]
                 {
                     ToothCondition.Healthy, ToothCondition.Decayed, ToothCondition.Filled,
                     ToothCondition.RootCanalTreated, ToothCondition.Crowned, ToothCondition.Extracted,
                     ToothCondition.Implanted,
                 })
        {
            panel.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 14, 4),
                Children =
                {
                    new Border
                    {
                        Width = 13, Height = 13, CornerRadius = new CornerRadius(3),
                        Background = Fill[condition],
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xDC, 0xE5)),
                        BorderThickness = new Thickness(1),
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    new TextBlock
                    {
                        Text = condition.ToString(),
                        FontSize = 11,
                        Foreground = (Brush)Application.Current.Resources["Brush.InkTertiary"],
                        Margin = new Thickness(6, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            });
        }

        return panel;
    }

    private UIElement BuildArchRow(string[] numbers, string archLabel)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        panel.Children.Add(new TextBlock
        {
            Text = archLabel,
            Width = 46,
            FontSize = 10.5,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["Brush.InkTertiary"],
        });

        foreach (var number in numbers)
        {
            panel.Children.Add(BuildTooth(number));
        }

        return panel;
    }

    private UIElement BuildTooth(string number)
    {
        var record = _records.FirstOrDefault(r => r.ToothNumber == number);
        var condition = record?.Condition ?? ToothCondition.Healthy;

        var label = new TextBlock
        {
            Text = number,
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["Brush.InkSecondary"],
        };

        var body = new Border
        {
            Width = 34,
            Height = 30,
            CornerRadius = new CornerRadius(6),
            Background = Fill[condition],
            BorderBrush = condition == ToothCondition.Healthy
                ? new SolidColorBrush(Color.FromRgb(0xD4, 0xDC, 0xE5))
                : new SolidColorBrush(Color.FromRgb(0x0E, 0x74, 0x90)),
            BorderThickness = new Thickness(condition == ToothCondition.Healthy ? 1 : 1.6),
            Child = label,
            Cursor = System.Windows.Input.Cursors.Hand,
        };

        var info = ToothCatalog.Find(number);
        var tooltip = number;
        if (info is not null)
        {
            tooltip = $"{info.Number} — {info.EnglishName} ({info.QuadrantLabel})";
            if (record is not null && !string.IsNullOrWhiteSpace(record.Notes))
            {
                tooltip += "\n" + record.Notes;
            }
        }

        body.ToolTip = tooltip;
        body.MouseLeftButtonDown += async (_, _) => await EditTooth(number, record);

        return new Border { Width = 36, Child = body };
    }

    private async Task EditTooth(string number, ToothRecord? existing)
    {
        var menu = new ContextMenu();

        foreach (var condition in Enum.GetValues<ToothCondition>())
        {
            var item = new MenuItem { Header = condition.ToString() };
            item.Click += async (_, _) =>
            {
                _visits.UpsertToothRecord(new ToothRecord
                {
                    PatientId = _patientId,
                    ToothNumber = number,
                    Condition = condition,
                    Notes = existing?.Notes ?? string.Empty,
                });

                await Reload(number, condition);
            };
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());

        var notesItem = new MenuItem { Header = "Add note…" };
        notesItem.Click += async (_, _) =>
        {
            var note = InputDialog.Show(
                Window.GetWindow(this),
                $"Tooth {number} note",
                "Clinical note for this tooth:",
                existing?.Notes ?? string.Empty);

            if (note is null)
            {
                return;
            }

            _visits.UpsertToothRecord(new ToothRecord
            {
                PatientId = _patientId,
                ToothNumber = number,
                Condition = existing?.Condition ?? ToothCondition.Healthy,
                Notes = note.Trim(),
            });

            await Reload(number, existing?.Condition ?? ToothCondition.Healthy);
        };
        menu.Items.Add(notesItem);

        menu.PlacementTarget = this;
        menu.IsOpen = true;
        await Task.CompletedTask;
    }

    private async Task Reload(string number, ToothCondition condition)
    {
        _records = await Task.Run(() => _visits.GetToothRecords(_patientId));
        BuildLayout(_records.Any(r => r.Dentition == Dentition.Deciduous));
        AppServices.Get<ToastService>().Info("Dental chart updated", $"Tooth {number} — {condition}.");
    }
}
