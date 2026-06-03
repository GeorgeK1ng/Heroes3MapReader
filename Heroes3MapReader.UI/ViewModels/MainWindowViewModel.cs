using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using Heroes3MapReader.Logic;
using Heroes3MapReader.Logic.Interfaces;
using Heroes3MapReader.Logic.Models;
using Heroes3MapReader.Logic.Models.Enums;
using Heroes3MapReader.Logic.Repositories;
using Heroes3MapReader.UI.Factories;
using Heroes3MapReader.UI.Views;

namespace Heroes3MapReader.UI.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMaps))]
    private string _directoryPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Select a directory to load maps";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLoadMaps))]
    [NotifyPropertyChangedFor(nameof(CanExportSelectedMaps))]
    [NotifyCanExecuteChangedFor(nameof(ExportSelectedMapsCommand))]
    private bool _isLoading;

    [ObservableProperty]
    private int? _selectedPlayerCount;

    [ObservableProperty]
    private int? _selectedTeamCount;

    [ObservableProperty]
    private MapDifficulty? _selectedDifficulty;

    [ObservableProperty]
    private VictoryConditionType? _selectedVictoryCondition;

    [ObservableProperty]
    private MapFormat? _selectedFormat;

    [ObservableProperty]
    private bool? _selectedHasUnderground;

    [ObservableProperty]
    private MapLanguage? _selectedDescriptionLanguage;

    [ObservableProperty]
    private bool _hideDuplicates;

    [ObservableProperty]
    private MapItemViewModel? _selectedMap;

    [ObservableProperty]
    private byte[]? _minimapImage;

    [ObservableProperty]
    private int _selectedSpellCount;

    [ObservableProperty]
    private ObservableCollection<SpellType> _bannedSpells = [];

    [ObservableProperty]
    private ObservableCollection<FactionType> _playableFactions = [];

    [ObservableProperty]
    private ObservableCollection<MapItemViewModel> _filteredMaps = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExportSelectedMaps))]
    [NotifyCanExecuteChangedFor(nameof(ExportSelectedMapsCommand))]
    private int _selectedMapCount;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<FactionFilterItemViewModel> FactionFilters { get; } = [];
    public ObservableCollection<SpellFilterItemViewModel> SpellFilters { get; } = [];
    public ObservableCollection<MapSizeFilterItemViewModel> MapSizeFilters { get; } = [];

    private readonly List<MapItemViewModel> _allMaps = [];
    private readonly List<MapItemViewModel> _selectedMaps = [];

    private readonly IMapReaderFactory _mapReaderFactory;
    private readonly IStorageProvider _storageProvider;
    private readonly ISpellSelectionWindowFactory _spellSelectionWindowFactory;
    private readonly ISettingsRepository _settingsRepository;
    private CancellationTokenSource? _filterCancellationTokenSource;
    private CancellationTokenSource? _debounceCancellationTokenSource;

    public MainWindowViewModel(
        IMapReaderFactory mapReaderFactory,
        IStorageProvider storageProvider,
        ISpellSelectionWindowFactory spellSelectionWindowFactory,
        ISettingsRepository settingsRepository)
    {
        _mapReaderFactory = mapReaderFactory;
        _storageProvider = storageProvider;
        _spellSelectionWindowFactory = spellSelectionWindowFactory;
        _settingsRepository = settingsRepository;
        PlayerCounts = Enumerable.Range(1, 8).Cast<int?>().Prepend(null).ToList();
        TeamCounts = Enumerable.Range(2, 6).Cast<int?>().Prepend(0).Prepend(null).ToList();
        Difficulties = Enum.GetValues<MapDifficulty>().Cast<MapDifficulty?>().Prepend(null).ToList();
        VictoryConditions = Enum.GetValues<VictoryConditionType>().Cast<VictoryConditionType?>().Prepend(null).ToList();
        MapFormats = Enum.GetValues<MapFormat>().Cast<MapFormat?>().Prepend(null).ToList();
        MapLanguages = Enum.GetValues<MapLanguage>().Cast<MapLanguage?>().Prepend(null).ToList();

        foreach (FactionType faction in Enum.GetValues<FactionType>())
        {
            var item = new FactionFilterItemViewModel(faction);
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FactionFilterItemViewModel.IsSelected))
                {
                    ApplyFiltersAndSort();
                }
            };
            FactionFilters.Add(item);
        }

        foreach (MapSize mapSize in Enum.GetValues<MapSize>())
        {
            var item = new MapSizeFilterItemViewModel(mapSize);
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MapSizeFilterItemViewModel.IsSelected))
                {
                    ApplyFiltersAndSort();
                }
            };
            MapSizeFilters.Add(item);
        }

        foreach (SpellType spell in Enum.GetValues<SpellType>())
        {
            var item = new SpellFilterItemViewModel(spell);
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SpellFilterItemViewModel.IsSelected))
                {
                    UpdateSelectedSpellCount();
                    ApplyFiltersAndSort();
                }
            };
            SpellFilters.Add(item);
        }

        LoadSettings();
    }

    private void LoadSettings()
    {
        AppSettings settings = _settingsRepository.LoadSettings();
        if (!string.IsNullOrWhiteSpace(settings.LastUsedDirectory) && Directory.Exists(settings.LastUsedDirectory))
        {
            DirectoryPath = settings.LastUsedDirectory;
        }
    }

    public List<int?> PlayerCounts { get; }
    public List<int?> TeamCounts { get; }
    public List<MapDifficulty?> Difficulties { get; }
    public List<VictoryConditionType?> VictoryConditions { get; }
    public List<MapFormat?> MapFormats { get; }
    public List<MapLanguage?> MapLanguages { get; }
    public List<bool?> HasUndergroundOptions { get; } = [null, true, false];

    public bool CanLoadMaps => !string.IsNullOrWhiteSpace(DirectoryPath) && !IsLoading;
    public bool CanExportSelectedMaps => SelectedMapCount > 0 && !IsLoading;

    partial void OnSelectedPlayerCountChanged(int? value)
    {
        ApplyFiltersAndSort();
    }

    partial void OnSelectedTeamCountChanged(int? value)
    {
        ApplyFiltersAndSort();
    }

    partial void OnSelectedDifficultyChanged(MapDifficulty? value)
    {
        ApplyFiltersAndSort();
    }

    partial void OnSelectedVictoryConditionChanged(VictoryConditionType? value)
    {
        ApplyFiltersAndSort();
    }

    partial void OnSelectedFormatChanged(MapFormat? value)
    {
        ApplyFiltersAndSort();
    }

    partial void OnSelectedHasUndergroundChanged(bool? value)
    {
        ApplyFiltersAndSort();
    }

    partial void OnSelectedDescriptionLanguageChanged(MapLanguage? value)
    {
        ApplyFiltersAndSort();
    }

    partial void OnHideDuplicatesChanged(bool value)
    {
        ApplyFiltersAndSort();
    }

    partial void OnSearchTextChanged(string value)
    {
        _debounceCancellationTokenSource?.Cancel();
        _debounceCancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = _debounceCancellationTokenSource.Token;

        Task.Delay(500, cancellationToken).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(ApplyFiltersAndSort);
            }
        },
        cancellationToken);
    }

    partial void OnSelectedMapChanged(MapItemViewModel? oldValue, MapItemViewModel? newValue)
    {
        if (oldValue != null)
        {
            // Unload terrain data to free memory
            oldValue.Map.SurfaceTerrain = null;
            oldValue.Map.UndergroundTerrain = null;
        }

        if (newValue == null)
        {
            MinimapImage = null;
            BannedSpells.Clear();
            PlayableFactions.Clear();
            return;
        }

        try
        {
            // Load terrain data if not already loaded
            if (newValue.Map.SurfaceTerrain == null || newValue.Map.UndergroundTerrain == null)
            {
                IMapReader reader = _mapReaderFactory.Create();
                MapInfo mapDetailsWithTerrain = reader.ReadMap(newValue.FilePath, true);
                newValue.Map.SurfaceTerrain = mapDetailsWithTerrain.SurfaceTerrain;
                newValue.Map.UndergroundTerrain = mapDetailsWithTerrain.UndergroundTerrain;
            }

            // Calculate banned spells (all spells minus available spells)
            BannedSpells.Clear();
            BannedSpells.AddRange(newValue.Map.BannedSpells.OrderBy(x => x.ToString()));

            // Get playable factions (factions that human players can play)
            List<FactionType> playableFactionsList = newValue.Map.Players
                .Where(p => p.CanBeHuman)
                .SelectMany(p => p.AllowedFactions)
                .Distinct()
                .OrderBy(f => f)
                .ToList();

            PlayableFactions.Clear();
            foreach (FactionType faction in playableFactionsList)
            {
                PlayableFactions.Add(faction);
            }

            GenerateMinimap(newValue);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading map details: {ex.Message}";
            MinimapImage = null;
        }
    }

    private void GenerateMinimap(MapItemViewModel mapViewModel)
    {
        try
        {
            var generator = new MinimapGenerator();
            MinimapImage = generator.GenerateMinimap(mapViewModel.Map, scale: 2, includeUnderground: mapViewModel.Map.HasUnderground);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating minimap: {ex.Message}";
            MinimapImage = null;
        }
    }

    [RelayCommand]
    private async Task BrowseDirectory()
    {
        var options = new FolderPickerOpenOptions
        {
            Title = "Select Map Directory",
            AllowMultiple = false,
        };

        IReadOnlyList<IStorageFolder> results = await _storageProvider.OpenFolderPickerAsync(options);

        if (results.Count > 0 && results[0].TryGetLocalPath() is string resultPath)
        {
            DirectoryPath = resultPath;
            StatusMessage = $"Selected directory: {resultPath}";
        }
        else
        {
            StatusMessage = "No directory selected.";
        }
    }

    [RelayCommand]
    private async Task LoadMaps()
    {
        if (string.IsNullOrWhiteSpace(DirectoryPath) || !Directory.Exists(DirectoryPath))
        {
            StatusMessage = "Invalid directory path";
            return;
        }

        SaveApplicationSettings();

        IsLoading = true;
        StatusMessage = "Scanning for maps...";
        _allMaps.Clear();
        SetSelectedMaps([]);
        FilteredMaps = [];

        try
        {
            string[] mapFiles = Directory.GetFiles(DirectoryPath, "*.h3m", SearchOption.AllDirectories);
            int totalFiles = mapFiles.Length;

            if (totalFiles == 0)
            {
                StatusMessage = "No map files found in directory";
                return;
            }

            var loadedCount = 0;
            var failedCount = 0;

            // Process maps on background thread
            await Task.Run(() =>
            {
                void ProcessMap(string mapFile, IMapReader mapReader)
                {
                    try
                    {
                        // Don't read terrain initially to save memory
                        MapInfo mapInfo = mapReader.ReadMap(mapFile, readTerrain: false);
                        var mapViewModel = new MapItemViewModel(mapFile, mapInfo);

                        // Update UI on UI thread
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            _allMaps.Add(mapViewModel);
                            loadedCount++;
                            StatusMessage = $"Loading maps... {loadedCount + failedCount}/{totalFiles}";
                        });
                    }
                    catch (Exception)
                    {
                        // Skip maps that fail to load - continue with the rest
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            failedCount++;
                            StatusMessage = $"Loading maps... {loadedCount + failedCount}/{totalFiles}";
                        });
                    }
                }

                if (totalFiles <= 100)
                {
                    IMapReader mapReader = _mapReaderFactory.Create();
                    for (int i = 0; i < totalFiles; i++)
                    {
                        ProcessMap(mapFiles[i], mapReader);
                    }
                }
                else
                {
                    int maxThreads = Math.Min(Environment.ProcessorCount, 16);
                    var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = maxThreads };

                    Parallel.ForEach(
                        mapFiles,
                        parallelOptions,
                        () => _mapReaderFactory.Create(),
                        (mapFile, _, mapReader) =>
                        {
                            ProcessMap(mapFile, mapReader);
                            return mapReader;
                        },
                        _ => { }
                    );
                }
            });

            // Wait a moment for final UI updates to complete
            _ = Task.Delay(100).ContinueWith(_ =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (failedCount > 0)
                    {
                        StatusMessage = $"Loaded {loadedCount} of {totalFiles} maps ({failedCount} failed)";
                    }
                    else
                    {
                        StatusMessage = $"Loaded {loadedCount} of {totalFiles} maps";
                    }
                });
            });



            ApplyFiltersAndSort();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading maps: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFiltersAndSort()
    {
        // Cancel any previous filtering operation
        _filterCancellationTokenSource?.Cancel();

        _filterCancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancellationToken = _filterCancellationTokenSource.Token;

        // Capture current filter values to use in background thread
        List<MapSize> selectedSizes = MapSizeFilters.Where(f => f.IsSelected).Select(f => f.MapSize).ToList();
        int? selectedPlayerCount = SelectedPlayerCount;
        int? selectedTeamCount = SelectedTeamCount;
        MapDifficulty? selectedDifficulty = SelectedDifficulty;
        VictoryConditionType? selectedVictoryCondition = SelectedVictoryCondition;
        MapFormat? selectedFormat = SelectedFormat;
        bool? selectedHasUnderground = SelectedHasUnderground;
        MapLanguage? selectedDescriptionLanguage = SelectedDescriptionLanguage;
        bool hideDuplicates = HideDuplicates;
        List<FactionType> selectedFactions = FactionFilters.Where(f => f.IsSelected).Select(f => f.Faction).ToList();
        List<SpellType> selectedSpells = SpellFilters.Where(f => f.IsSelected).Select(f => f.Spell).ToList();
        string searchText = SearchText;
        List<MapItemViewModel> allMapsCopy = _allMaps.ToList();
        MapItemViewModel? currentSelectedMap = SelectedMap;

        _ = Task.Run(() =>
        {
            IEnumerable<MapItemViewModel> filtered = allMapsCopy.AsEnumerable();

            if (selectedSizes.Count > 0)
            {
                filtered = filtered.Where(m => selectedSizes.Contains(m.Map.Size));
            }

            if (selectedPlayerCount.HasValue)
            {
                filtered = filtered.Where(m => m.Map.PlayerCount == selectedPlayerCount.Value);
            }

            if (selectedTeamCount.HasValue)
            {
                filtered = filtered.Where(m => m.Map.TeamCount == selectedTeamCount.Value);
            }

            if (selectedDifficulty.HasValue)
            {
                filtered = filtered.Where(m => m.Map.Difficulty == selectedDifficulty.Value);
            }

            if (selectedVictoryCondition.HasValue)
            {
                filtered = filtered.Where(m => (m.Map.VictoryCondition?.Type ?? VictoryConditionType.Standard) == selectedVictoryCondition.Value);
            }

            if (selectedFormat.HasValue)
            {
                filtered = filtered.Where(m => m.Map.Format == selectedFormat.Value);
            }

            if (selectedHasUnderground.HasValue)
            {
                filtered = filtered.Where(m => m.Map.HasUnderground == selectedHasUnderground.Value);
            }

            if (selectedDescriptionLanguage.HasValue)
            {
                filtered = filtered.Where(m => m.Map.DescriptionLanguage == selectedDescriptionLanguage.Value);
            }

            if (selectedFactions.Count > 0)
            {
                filtered = filtered.Where(m =>
                {
                    return selectedFactions.All(faction =>
                        m.Map.Players.Any(p => p.CanBeHuman && (p.AllFactionsAllowed || p.AllowedFactions.Contains(faction)))
                    );
                });
            }

            if (selectedSpells.Count > 0)
            {
                filtered = filtered.Where(m =>
                {
                    return selectedSpells.All(spell => !m.Map.BannedSpells.Contains(spell));
                });
            }

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(m =>
                    (m.Map.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true) ||
                    (m.Map.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true)
                );
            }

            if (hideDuplicates)
            {
                filtered = filtered
                    .GroupBy(m => GetDuplicateKey(m.Map.Name, m.Map.Description), StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First());
            }

            List<MapItemViewModel> result = filtered.ToList();

            cancellationToken.ThrowIfCancellationRequested();

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Update UI by replacing the entire collection (single notification)
                FilteredMaps = new ObservableCollection<MapItemViewModel>(result);

                if (currentSelectedMap != null && result.Contains(currentSelectedMap))
                {
                    SelectedMap = currentSelectedMap;
                }

                int filterCount = allMapsCopy.Count - result.Count;
                if (filterCount > 0)
                {
                    StatusMessage = $"Showing {FilteredMaps.Count} of {allMapsCopy.Count} maps ({filterCount} filtered)";
                }
                else if (allMapsCopy.Count > 0)
                {
                    StatusMessage = $"Showing all {allMapsCopy.Count} maps";
                }
            });
        }, cancellationToken);
    }

    public void SetSelectedMaps(IEnumerable<MapItemViewModel> selectedMaps)
    {
        _selectedMaps.Clear();
        _selectedMaps.AddRange(selectedMaps);
        SelectedMapCount = _selectedMaps.Count;
    }

    [RelayCommand(CanExecute = nameof(CanExportSelectedMaps))]
    private async Task ExportSelectedMaps()
    {
        List<MapItemViewModel> mapsToExport = _selectedMaps.ToList();
        if (mapsToExport.Count == 0)
        {
            StatusMessage = "No maps selected for export.";
            return;
        }

        var options = new FolderPickerOpenOptions
        {
            Title = "Select Export Directory",
            AllowMultiple = false,
        };

        IReadOnlyList<IStorageFolder> results = await _storageProvider.OpenFolderPickerAsync(options);
        if (results.Count == 0 || results[0].TryGetLocalPath() is not string exportRoot)
        {
            StatusMessage = "Export cancelled.";
            return;
        }

        try
        {
            int exportedCount = await Task.Run(() => ExportMaps(mapsToExport, exportRoot));
            StatusMessage = $"Exported {exportedCount} map{(exportedCount == 1 ? string.Empty : "s")}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error exporting maps: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedPlayerCount = null;
        SelectedTeamCount = null;
        SelectedDifficulty = null;
        SelectedVictoryCondition = null;
        SelectedFormat = null;
        SelectedHasUnderground = null;
        SelectedDescriptionLanguage = null;
        HideDuplicates = false;

        foreach (FactionFilterItemViewModel filter in FactionFilters)
        {
            filter.IsSelected = false;
        }

        foreach (SpellFilterItemViewModel filter in SpellFilters)
        {
            filter.IsSelected = false;
        }

        foreach (MapSizeFilterItemViewModel filter in MapSizeFilters)
        {
            filter.IsSelected = false;
        }

        UpdateSelectedSpellCount();
        ApplyFiltersAndSort();
    }

    [RelayCommand]
    private async Task OpenSpellSelection(Window? parentWindow)
    {
        if (parentWindow == null)
        {
            return;
        }

        SpellSelectionWindow spellWindow = _spellSelectionWindowFactory.Create();
        var viewModel = (SpellSelectionWindowViewModel)spellWindow.DataContext!;
        viewModel.SetSelectedSpells(SpellFilters);

        await spellWindow.ShowDialog(parentWindow);

        // Update main window filters from dialog selections
        foreach (SpellFilterItemViewModel dialogSpell in viewModel.SpellFilters)
        {
            SpellFilterItemViewModel? mainSpell = SpellFilters.FirstOrDefault(s => s.Spell == dialogSpell.Spell);
            if (mainSpell != null)
            {
                mainSpell.IsSelected = dialogSpell.IsSelected;
            }
        }

        UpdateSelectedSpellCount();
    }

    private void UpdateSelectedSpellCount()
    {
        SelectedSpellCount = SpellFilters.Count(f => f.IsSelected);
    }

    private static int ExportMaps(IEnumerable<MapItemViewModel> mapsToExport, string exportRoot)
    {
        Directory.CreateDirectory(exportRoot);

        var exportedCount = 0;
        foreach (MapItemViewModel map in mapsToExport)
        {
            string languageDirectory = Path.Combine(exportRoot, GetLanguageFolderName(map.Map.DescriptionLanguage));
            Directory.CreateDirectory(languageDirectory);

            string extension = Path.GetExtension(map.FilePath);
            string mapName = string.IsNullOrWhiteSpace(map.Map.Name)
                ? Path.GetFileNameWithoutExtension(map.FilePath)
                : map.Map.Name;
            string fileName = $"[{GetMapFormatPrefix(map.Map.Format)}] - {SanitizeFileName(mapName)}{extension}";
            string destinationPath = GetUniqueDestinationPath(languageDirectory, fileName);

            File.Copy(map.FilePath, destinationPath);
            exportedCount++;
        }

        return exportedCount;
    }

    private static string GetMapFormatPrefix(MapFormat format)
    {
        return format switch
        {
            MapFormat.RoE => "RoE",
            MapFormat.AB => "AB",
            MapFormat.SoD => "SoD",
            MapFormat.WoG => "WoG",
            MapFormat.HotA => "HotA",
            _ => format.ToString(),
        };
    }

    private static string GetLanguageFolderName(MapLanguage language)
    {
        return language.ToString();
    }

    private static string SanitizeFileName(string fileName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new(fileName.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown map" : sanitized.Trim();
    }

    private static string GetUniqueDestinationPath(string directory, string fileName)
    {
        string destinationPath = Path.Combine(directory, fileName);
        if (!File.Exists(destinationPath))
        {
            return destinationPath;
        }

        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        var counter = 2;

        do
        {
            destinationPath = Path.Combine(directory, $"{baseName} ({counter}){extension}");
            counter++;
        }
        while (File.Exists(destinationPath));

        return destinationPath;
    }

    private static string GetDuplicateKey(string? name, string? description)
    {
        return $"{NormalizeDuplicateText(name)}\n{NormalizeDuplicateText(description)}";
    }

    private static string NormalizeDuplicateText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private void SaveApplicationSettings()
    {
        var settings = new AppSettings
        {
            LastUsedDirectory = DirectoryPath,
        };
        _settingsRepository.SaveSettings(settings);
    }
}
