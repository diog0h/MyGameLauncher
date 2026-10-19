using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using GameLauncher.Localization;
using GameLauncher.Models;
using GameLauncher.Services;

namespace GameLauncher.Views;

public partial class PlaytimePage : UserControl, IPage
{
    public string Title => Loc.T("Nav_Playtime");

    private AppState State => AppState.Current;

    public PlaytimePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            State.SessionsChanged += Refresh;
            State.GamesLoaded += Refresh;
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            State.SessionsChanged -= Refresh;
            State.GamesLoaded -= Refresh;
        };
    }

    public void OnReturned() => Refresh();

    private sealed record DayBar(string Label, string ValueText, double BarHeight, double BarOpacity, string Tooltip);
    private sealed record TopRow(int Rank, Game Game, double BarWidth);

    private void Refresh()
    {
        var games = State.Games;

        // ---- resumo
        int total = games.Sum(g => g.TotalPlaytimeMinutes);
        TotalText.Text = FormatHours(total);

        var since = DateTime.Today.AddDays(-6);
        var weekSessions = State.Data.Sessions.Where(s => s.End >= since).ToList();
        int weekMin = (int)(weekSessions.Sum(s => SecondsWithin(s, since, DateTime.Now)) / 60);
        WeekText.Text = FormatHours(weekMin);
        WeekSub.Text = weekSessions.Count == 1 ? Loc.T("Pt_OneSession") : Loc.F("Pt_NSessions", weekSessions.Count);

        var top = games.Where(g => g.TotalPlaytimeMinutes > 0).OrderByDescending(g => g.TotalPlaytimeMinutes).ToList();
        TopName.Text = top.FirstOrDefault()?.Name ?? "—";
        TopTime.Text = top.FirstOrDefault()?.PlaytimeText ?? "";

        // ---- gráfico: minutos por dia (uma sessão que passa da meia-noite é dividida pelos dois dias)
        var days = Enumerable.Range(0, 7).Select(i => since.AddDays(i)).ToList();
        var minutesPerDay = days.Select(d =>
            State.Data.Sessions.Sum(s => SecondsWithin(s, d, d.AddDays(1))) / 60).ToList();
        double max = Math.Max(60, minutesPerDay.Max());

        DaysChart.ItemsSource = days.Select((d, i) =>
        {
            var m = minutesPerDay[i];
            var label = d == DateTime.Today ? Loc.T("Today") : d.ToString("ddd", Loc.Culture).TrimEnd('.');
            var perGame = State.Data.Sessions
                .Where(s => SecondsWithin(s, d, d.AddDays(1)) > 0)
                .GroupBy(s => s.GameId)
                .Select(g => $"{State.FindGame(g.Key)?.Name ?? g.Key}: {Game.FormatMinutes((int)(g.Sum(s => SecondsWithin(s, d, d.AddDays(1))) / 60))}");
            var tip = d.ToString("dddd, d MMM", Loc.Culture) + "\n" + (m > 0 ? string.Join("\n", perGame) : Loc.T("Pt_NoSessionsDay"));
            return new DayBar(label, m >= 1 ? ShortTime((int)m) : "", Math.Max(4, m / max * 160), m > 0 ? 1 : 0.25, tip);
        }).ToList();

        // ---- lista
        int topMax = top.FirstOrDefault()?.TotalPlaytimeMinutes ?? 1;
        TopList.ItemsSource = top.Take(15).Select((g, i) =>
            new TopRow(i + 1, g, Math.Max(4, (double)g.TotalPlaytimeMinutes / topMax * 520))).ToList();
        NoTopText.Visibility = top.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static double SecondsWithin(PlaySession s, DateTime from, DateTime to)
    {
        var a = s.Start > from ? s.Start : from;
        var b = s.End < to ? s.End : to;
        return Math.Max(0, (b - a).TotalSeconds);
    }

    private static string FormatHours(int minutes) =>
        minutes < 60 ? $"{minutes} min" : $"{(minutes / 60.0).ToString("0.#", Loc.Culture)} h";

    private static string ShortTime(int minutes) =>
        minutes < 60 ? $"{minutes}m" : $"{(minutes / 60.0).ToString("0.#", Loc.Culture)}h";
}
