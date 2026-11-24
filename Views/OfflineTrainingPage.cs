using System.Globalization;
using MomentumGroves.Mobile.Configuration;
using MomentumGroves.Mobile.Data;
using MomentumGroves.Mobile.Service;
using static MomentumGroves.Mobile.DTO.TrainingData;

namespace MomentumGroves.Mobile.Views;

public sealed class OfflineTrainingPage : ContentPage
{
    private readonly TrainingSyncService _sync;
    private readonly ITrainingStore _store;
    private readonly DataProvider _provider;
    private readonly Picker _groups = new() { Title = "Programme group", TextColor = Colors.White };
    private readonly VerticalStackLayout _programmes = new() { Spacing = 12 };
    private readonly Label _status = Text("", color: "#9ba9ac");
    private readonly Button _discard = Action("Discard unresolved phone edits");
    private readonly Button _editor = Action("Open full editor (online)");
    private readonly Dictionary<string, (string Text, decimal BaseKg)> _drafts = new();
    private TrainingState _state = new(null, []);

    public OfflineTrainingPage(TrainingSyncService sync, ITrainingStore store, DataProvider provider)
    {
        _sync = sync;
        _store = store;
        _provider = provider;
        var refresh = Action("Refresh programmes");
        refresh.Clicked += async (_, _) => await RefreshAsync(reviewDrafts: true);
        Title = "Training";
        BackgroundColor = Color.FromArgb("#101417");
        _groups.SelectedIndexChanged += (_, _) => ShowGroup();
        _discard.Clicked += async (_, _) =>
        {
            if (!await DisplayAlert("Discard unresolved weights?", "Unresolved phone edits will be discarded.",
                    "Discard", "Cancel"))
                return;

            try
            {
                await _store.DiscardConflictsAsync();
                await ReloadAsync();
                ShowStatus();
            }
            catch (Exception)
            {
                _status.Text = "Couldn't update the training data on this phone.";
            }
        };
        _editor.Clicked += async (_, _) =>
        {
            if (_sync.IsConfigured)
                await Navigation.PushAsync(new TrainingPage(_sync.EditorAddress));
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16, Spacing = 12,
                Children = { Text("Training programmes", 24), _status, refresh, _groups, _programmes, _discard, _editor }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync(bool reviewDrafts = false)
    {
        try
        {
            await ReloadAsync(reviewDrafts);
            ShowStatus();
        }
        catch (Exception exception)
        {
            _groups.ItemsSource = null;
            _programmes.Children.Clear();
            _discard.IsVisible = false;
            _status.Text = exception is DataStoreException ? exception.Message : "Couldn't load training data.";
        }
    }

    private async Task ReloadAsync(bool reviewDrafts = false)
    {
        var selection = (_groups.SelectedItem as TrainingGroup)?.Id;
        _state = await _store.ReadAsync();
        var groups = _state.Programmes?.Groups ?? [];
        if (reviewDrafts)
        {
            foreach (var exercise in groups.SelectMany(group => group.Programmes).SelectMany(programme => programme.Exercises))
            {
                if (_drafts.TryGetValue(exercise.Id, out var draft))
                    _drafts[exercise.Id] = (draft.Text, exercise.Kg);
            }
        }
        _groups.IsVisible = groups.Count > 0;
        _editor.IsVisible = !_sync.UsesPrivateCertificate;
        _editor.IsEnabled = _sync.IsConfigured;
        _discard.IsVisible = _state.Pending.Any(edit => edit.Status is not null);
        _groups.ItemDisplayBinding = new Binding(nameof(TrainingGroup.Name));
        _groups.ItemsSource = groups;
        _groups.SelectedIndex = groups.Count == 0 ? -1 : Math.Max(0, groups.FindIndex(group => group.Id == selection));
        ShowGroup();
    }

    private void ShowStatus(string? message = null)
    {
        var detail = _provider.IsCloud
            ? "Shared training programmes. The full editor still requires the PC."
            : !_sync.IsConfigured
            ? "Open Sync to PC from the home screen to pair and download your programmes."
            : _state.Programmes is null
                ? "Open Sync to PC from the home screen and tap Sync now to download your programmes."
                : _state.Programmes.Groups.Count == 0
                    ? "No saved programmes yet. Create one in the full editor."
                    : "Your programmes are saved on this phone.";
        var pending = _state.Pending.Count(edit => edit.Status is null);
        var conflicts = _state.Pending.Count - pending;
        _status.Text = string.Join("\n", new[]
        {
            message ?? detail,
            pending > 0 ? $"Weight changes waiting to sync: {pending}." : null,
            conflicts > 0 ? $"Weights to review: {conflicts}. The PC changed or removed these exercises." : null
        }.OfType<string>());
    }

    private void ShowGroup()
    {
        _programmes.Children.Clear();
        if (_groups.SelectedItem is not TrainingGroup group)
            return;

        foreach (var programme in group.Programmes)
        {
            var section = new VerticalStackLayout { Spacing = 8 };
            section.Children.Add(Text(programme.Title, 20));
            if (!string.IsNullOrWhiteSpace(programme.Subtitle))
                section.Children.Add(Text(programme.Subtitle, color: "#9ba9ac"));

            foreach (var exercise in programme.Exercises)
                section.Children.Add(ExerciseCard(exercise));
            _programmes.Children.Add(section);
        }
    }

    private Border ExerciseCard(Exercise exercise)
    {
        var cardio = exercise.Type == "cardio";
        var content = new VerticalStackLayout { Spacing = 6 };
        content.Children.Add(Text(exercise.Name, 17));
        var cachedImage = exercise.Image is { } reference
            ? _state.CloudImages?.GetValueOrDefault(reference) ?? _sync.CachedImage(reference)
            : null;
        if (cachedImage is { } image)
            content.Children.Add(new Image { Source = ImageSource.FromFile(image), HeightRequest = 140, Aspect = Aspect.AspectFit });
        content.Children.Add(Text(cardio ? $"{exercise.Minutes} min" : $"{exercise.Sets} sets · {exercise.Reps} reps",
            color: "#9ba9ac"));
        if (!string.IsNullOrWhiteSpace(exercise.Description))
            content.Children.Add(Text(exercise.Description, color: "#9ba9ac"));

        if (!cardio)
        {
            var edit = _state.Pending.Find(item => item.ExerciseId == exercise.Id);
            var entry = new Entry
            {
                Text = _drafts.TryGetValue(exercise.Id, out var draft)
                    ? draft.Text
                    : (edit?.Kg ?? exercise.Kg).ToString(CultureInfo.CurrentCulture),
                Keyboard = Keyboard.Numeric, TextColor = Colors.White
            };
            SemanticProperties.SetDescription(entry, $"Weight in kilograms for {exercise.Name}");
            entry.TextChanged += (_, args) =>
            {
                var baseline = _drafts.TryGetValue(exercise.Id, out var previous) ? previous.BaseKg : exercise.Kg;
                _drafts[exercise.Id] = (args.NewTextValue ?? "", baseline);
            };
            var save = Action(edit?.Status == "conflict" ? "Save this weight" : "Save weight");
            if (edit?.Status == "conflict")
                content.Children.Add(Text($"PC weight: {exercise.Kg} kg. Save this weight to replace it.", color: "#ecd08c"));
            else if (_provider.IsCloud && _drafts.ContainsKey(exercise.Id))
                content.Children.Add(Text($"Saved weight: {exercise.Kg} kg", color: "#ecd08c"));

            save.Clicked += async (_, _) =>
            {
                if (!decimal.TryParse(entry.Text?.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out var kg))
                {
                    await DisplayAlert("Invalid weight", "Enter a weight of zero or more kilograms.", "OK");
                    return;
                }

                save.IsEnabled = false;
                try
                {
                    var baseline = edit?.Status != "conflict" && _drafts.TryGetValue(exercise.Id, out var input)
                        ? input.BaseKg : exercise.Kg;
                    await _store.SaveWeightAsync(exercise.Id, kg, baseline);
                    _drafts.Remove(exercise.Id);
                    await ReloadAsync();
                    ShowStatus(_provider.TrainingSaved);
                }
                catch (DataStoreException exception)
                {
                    _status.Text = exception.Message;
                }
                catch (Exception)
                {
                    _status.Text = "Couldn't complete the weight update. Check the saved weight and try again.";
                }
                finally
                {
                    save.IsEnabled = true;
                }
            };
            var row = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) },
                ColumnSpacing = 8 };
            row.Add(entry, 0);
            row.Add(Text("kg"), 1);
            row.Add(save, 2);
            content.Children.Add(row);
        }

        return new Border
        {
            BackgroundColor = Color.FromArgb("#1a2025"), Stroke = Color.FromArgb("#303a40"), Padding = 12,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Content = content
        };
    }

    private static Label Text(string value, double size = 14, string color = "#eef2f1") =>
        new() { Text = value, FontSize = size, TextColor = Color.FromArgb(color) };

    private static Button Action(string value) =>
        new() { Text = value, BackgroundColor = Color.FromArgb("#27312b"), TextColor = Colors.White };
}
