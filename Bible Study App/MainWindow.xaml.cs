using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Bible_Study_App;

public partial class MainWindow : Window
{
    private const string BackendApiUrl = "http://localhost:5055";
    private const string DefaultOverviewPrompt = "Summarize this study and give one clear next step.";
    private static readonly HttpClient BackendHttpClient = new();
    private static readonly Dictionary<string, int[]> BibleVerseCounts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Genesis"] = [31, 25, 24, 26, 32, 22, 24, 22, 29, 32, 32, 20, 18, 24, 21, 16, 27, 33, 38, 18, 34, 24, 20, 67, 34, 35, 46, 22, 35, 43, 55, 32, 20, 31, 29, 43, 36, 30, 23, 23, 57, 38, 34, 34, 28, 34, 31, 22, 33, 26],
        ["Exodus"] = [22, 25, 22, 31, 23, 30, 25, 32, 35, 29, 10, 51, 22, 31, 27, 36, 16, 27, 25, 26, 36, 31, 33, 18, 40, 37, 21, 43, 46, 38, 18, 35, 23, 35, 35, 38, 29, 31, 43, 38],
        ["Leviticus"] = [17, 16, 17, 35, 19, 30, 38, 36, 24, 20, 47, 8, 59, 57, 33, 34, 16, 30, 37, 27, 24, 33, 44, 23, 55, 46, 34],
        ["Numbers"] = [54, 34, 51, 49, 31, 27, 89, 26, 23, 36, 35, 16, 33, 45, 41, 50, 13, 32, 22, 29, 35, 41, 30, 25, 18, 65, 23, 31, 40, 16, 54, 42, 56, 29, 34, 13],
        ["Deuteronomy"] = [46, 37, 29, 49, 33, 25, 26, 20, 29, 22, 32, 32, 18, 29, 23, 22, 20, 22, 21, 20, 23, 30, 25, 22, 19, 19, 26, 68, 29, 20, 30, 52, 29, 12],
        ["Joshua"] = [18, 24, 17, 24, 15, 27, 26, 35, 27, 43, 23, 24, 33, 15, 63, 10, 18, 28, 51, 9, 45, 34, 16, 33],
        ["Judges"] = [36, 23, 31, 24, 31, 40, 25, 35, 57, 18, 40, 15, 25, 20, 20, 31, 13, 31, 30, 48, 25],
        ["Ruth"] = [22, 23, 18, 22],
        ["1 Samuel"] = [28, 36, 21, 22, 12, 21, 17, 22, 27, 27, 15, 25, 23, 52, 35, 23, 58, 30, 24, 42, 15, 23, 29, 22, 44, 25, 12, 25, 11, 31, 13],
        ["2 Samuel"] = [27, 32, 39, 12, 25, 23, 29, 18, 13, 19, 27, 31, 39, 33, 37, 23, 29, 33, 43, 26, 22, 51, 39, 25],
        ["1 Kings"] = [53, 46, 28, 34, 18, 38, 51, 66, 28, 29, 43, 33, 34, 31, 34, 34, 24, 46, 21, 43, 29, 53],
        ["2 Kings"] = [18, 25, 27, 44, 27, 33, 20, 29, 37, 36, 21, 21, 25, 29, 38, 20, 41, 37, 37, 21, 26, 20, 37, 20, 30],
        ["1 Chronicles"] = [54, 55, 24, 43, 26, 81, 40, 40, 44, 14, 47, 40, 14, 17, 29, 43, 27, 17, 19, 8, 30, 19, 32, 31, 31, 32, 34, 21, 30],
        ["2 Chronicles"] = [17, 18, 17, 22, 14, 42, 22, 18, 31, 19, 23, 16, 22, 15, 19, 14, 19, 34, 11, 37, 20, 12, 21, 27, 28, 23, 9, 27, 36, 27, 21, 33, 25, 33, 27, 23],
        ["Ezra"] = [11, 70, 13, 24, 17, 22, 28, 36, 15, 44],
        ["Nehemiah"] = [11, 20, 32, 23, 19, 19, 73, 18, 38, 39, 36, 47, 31],
        ["Esther"] = [22, 23, 15, 17, 14, 14, 10, 17, 32, 3],
        ["Job"] = [22, 13, 26, 21, 27, 30, 21, 22, 35, 22, 20, 25, 28, 22, 35, 22, 16, 21, 29, 29, 34, 30, 17, 25, 6, 14, 23, 28, 25, 31, 40, 22, 33, 37, 16, 33, 24, 41, 30, 24, 34, 17],
        ["Psalms"] = [6, 12, 8, 8, 12, 10, 17, 9, 20, 18, 7, 8, 6, 7, 5, 11, 15, 50, 14, 9, 13, 31, 6, 10, 22, 12, 14, 9, 11, 12, 24, 11, 22, 22, 28, 12, 40, 22, 13, 17, 13, 11, 5, 26, 17, 11, 9, 14, 20, 23, 19, 9, 6, 7, 23, 13, 11, 11, 17, 12, 8, 12, 11, 10, 13, 20, 7, 35, 36, 5, 24, 20, 28, 23, 10, 12, 20, 72, 13, 19, 16, 8, 18, 12, 13, 17, 7, 18, 52, 17, 16, 15, 5, 23, 11, 13, 12, 9, 9, 5, 8, 28, 22, 35, 45, 48, 43, 13, 31, 7, 10, 10, 9, 8, 18, 19, 2, 29, 176, 7, 8, 9, 4, 8, 5, 6, 5, 6, 8, 8, 3, 18, 3, 3, 21, 26, 9, 8, 24, 13, 10, 7, 12, 15, 21, 10, 20, 14, 9, 6],
        ["Proverbs"] = [33, 22, 35, 27, 23, 35, 27, 36, 18, 32, 31, 28, 25, 35, 33, 33, 28, 24, 29, 30, 31, 29, 35, 34, 28, 28, 27, 28, 27, 33, 31],
        ["Ecclesiastes"] = [18, 26, 22, 16, 20, 12, 29, 17, 18, 20, 10, 14],
        ["Song of Solomon"] = [17, 17, 11, 16, 16, 13, 13, 14],
        ["Isaiah"] = [31, 22, 26, 6, 30, 13, 25, 22, 21, 34, 16, 6, 22, 32, 9, 14, 14, 7, 25, 6, 17, 25, 18, 23, 12, 21, 13, 29, 24, 33, 9, 20, 24, 17, 10, 22, 38, 22, 8, 31, 29, 25, 28, 28, 25, 13, 15, 22, 26, 11, 23, 15, 12, 17, 13, 12, 21, 14, 21, 22, 11, 12, 19, 12, 25, 24],
        ["Jeremiah"] = [19, 37, 25, 31, 31, 30, 34, 22, 26, 25, 23, 17, 27, 22, 21, 21, 27, 23, 15, 18, 14, 30, 40, 10, 38, 24, 22, 17, 32, 24, 40, 44, 26, 22, 19, 32, 21, 28, 18, 16, 18, 22, 13, 30, 5, 28, 7, 47, 39, 46, 64, 34],
        ["Lamentations"] = [22, 22, 66, 22, 22],
        ["Ezekiel"] = [28, 10, 27, 17, 17, 14, 27, 18, 11, 22, 25, 28, 23, 23, 8, 63, 24, 32, 14, 49, 32, 31, 49, 27, 17, 21, 36, 26, 21, 26, 18, 32, 33, 31, 15, 38, 28, 23, 29, 49, 26, 20, 27, 31, 25, 24, 23, 35],
        ["Daniel"] = [21, 49, 30, 37, 31, 28, 28, 27, 27, 21, 45, 13],
        ["Hosea"] = [11, 23, 5, 19, 15, 11, 16, 14, 17, 15, 12, 14, 16, 9],
        ["Joel"] = [20, 32, 21],
        ["Amos"] = [15, 16, 15, 13, 27, 14, 17, 14, 15],
        ["Obadiah"] = [21],
        ["Jonah"] = [17, 10, 10, 11],
        ["Micah"] = [16, 13, 12, 13, 15, 16, 20],
        ["Nahum"] = [15, 13, 19],
        ["Habakkuk"] = [17, 20, 19],
        ["Zephaniah"] = [18, 15, 20],
        ["Haggai"] = [15, 23],
        ["Zechariah"] = [21, 13, 10, 14, 11, 15, 14, 23, 17, 12, 17, 14, 9, 21],
        ["Malachi"] = [14, 17, 18, 6],
        ["Matthew"] = [25, 23, 17, 25, 48, 34, 29, 34, 38, 42, 30, 50, 58, 36, 39, 28, 27, 35, 30, 34, 46, 46, 39, 51, 46, 75, 66, 20],
        ["Mark"] = [45, 28, 35, 41, 43, 56, 37, 38, 50, 52, 33, 44, 37, 72, 47, 20],
        ["Luke"] = [80, 52, 38, 44, 39, 49, 50, 56, 62, 42, 54, 59, 35, 35, 32, 31, 37, 43, 48, 47, 38, 71, 56, 53],
        ["John"] = [51, 25, 36, 54, 47, 71, 53, 59, 41, 42, 57, 50, 38, 31, 27, 33, 26, 40, 42, 31, 25],
        ["Acts"] = [26, 47, 26, 37, 42, 15, 60, 40, 43, 48, 30, 25, 52, 28, 41, 40, 34, 28, 41, 38, 40, 30, 35, 27, 27, 32, 44, 31],
        ["Romans"] = [32, 29, 31, 25, 21, 23, 25, 39, 33, 21, 36, 21, 14, 23, 33, 27],
        ["1 Corinthians"] = [31, 16, 23, 21, 13, 20, 40, 13, 27, 33, 34, 31, 13, 40, 58, 24],
        ["2 Corinthians"] = [24, 17, 18, 18, 21, 18, 16, 24, 15, 18, 33, 21, 14],
        ["Galatians"] = [24, 21, 29, 31, 26, 18],
        ["Ephesians"] = [23, 22, 21, 32, 33, 24],
        ["Philippians"] = [30, 30, 21, 23],
        ["Colossians"] = [29, 23, 25, 18],
        ["1 Thessalonians"] = [10, 20, 13, 18, 28],
        ["2 Thessalonians"] = [12, 17, 18],
        ["1 Timothy"] = [20, 15, 16, 16, 25, 21],
        ["2 Timothy"] = [18, 26, 17, 22],
        ["Titus"] = [16, 15, 15],
        ["Philemon"] = [25],
        ["Hebrews"] = [14, 18, 19, 16, 14, 20, 28, 13, 28, 39, 40, 29, 25],
        ["James"] = [27, 26, 18, 17, 20],
        ["1 Peter"] = [25, 25, 22, 19, 14],
        ["2 Peter"] = [21, 22, 18],
        ["1 John"] = [10, 29, 24, 21, 21],
        ["2 John"] = [13],
        ["3 John"] = [14],
        ["Jude"] = [25],
        ["Revelation"] = [20, 29, 22, 11, 14, 17, 17, 13, 21, 11, 19, 17, 18, 20, 8, 21, 18, 24, 21, 15, 27, 21]
    };
    private readonly Dictionary<string, WorkspaceItem> _workspaceByBook = new();
    private readonly ObservableCollection<EditorCommandOption> _filteredSlashCommands = new();
    private readonly Brush[] _accentPalette;
    private readonly List<EditorCommandOption> _slashCommands;
    private readonly DispatcherTimer _slashCommandRefreshTimer;
    private readonly DispatcherTimer _workspaceSaveTimer;
    private readonly DispatcherTimer _toastTimer;
    private readonly Dictionary<ScrollViewer, DispatcherTimer> _scrollBarRevealTimers = new();
    private readonly string _workspaceFilePath;
    private readonly ObservableCollection<DailyStudyEntry> _todayEditedStudies = new();
    private readonly ObservableCollection<ScheduledNotificationDisplay> _scheduledNotificationDisplays = new();
    private readonly ObservableCollection<ReminderScheduleItem> _reminderScheduleItems = new();
    private readonly ObservableCollection<ReminderPreset> _reminderPresets = new();
    private readonly ObservableCollection<ColorSchemePreset> _colorSchemePresets = new();
    private readonly ObservableCollection<ScriptureVerseDisplay> _scriptureVerses = new();
    private readonly List<ScheduledNotificationState> _scheduledNotifications = new();
    private readonly Dictionary<string, DailyNoteState> _dailyNotesByDate = new();
    private readonly Dictionary<string, Dictionary<string, List<string>>> _scriptureTextByBookChapter = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, Paragraph> _scriptureParagraphsByVerse = new();
    private readonly Dictionary<ScriptureReferenceKey, List<TagntWordEntry>> _tagntEntriesByReference = new();
    private readonly Dictionary<string, GreekLexiconEntry> _greekLexiconByStrong = new(StringComparer.OrdinalIgnoreCase);
    private static readonly GreekLexiconEntry EmptyGreekLexiconEntry = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    private enum DefinitionLineKind { Lead, Main, Sub }
    private sealed record DefinitionLine(DefinitionLineKind Kind, string Marker, string Text);
    private enum MovablePanelKind { Scripture, Strongs }
    private enum PanelPreset { Top, Left, Right, Bottom, Float }
    private static readonly Brush PanelPresetIdleBrush = BrushFrom("#E6DDAA");
    private static readonly Brush PanelPresetIdleBorderBrush = BrushFrom("#111111");
    private static readonly Brush PanelPresetActiveBrush = BrushFrom("#B7C98B");
    private static readonly Brush PanelPresetActiveBorderBrush = BrushFrom("#606C38");
    private string _scriptureTranslationLabel = "ASV";
    private string? _scriptureVisibleBookName;
    private int? _scriptureVisibleChapter;
    private BibleBook? _selectedBook;
    private WorkspaceItem? _currentContainer;
    private WorkspaceItem? _currentStudy;
    private StudyBlock? _activeStudyBlock;
    private TextBox? _activeStudyTextBox;
    private string _selectedColorSchemeName = "Olive Study";
    private AppNavTab _activeNavTab = AppNavTab.BibleStudy;
    private string _lastSlashQuery = string.Empty;
    private double _bibleBooksSmoothScrollTargetOffset;
    private DateTime _bibleBooksSmoothScrollLastFrame;
    private bool _bibleBooksSmoothScrollActive;
    private double _scriptureSmoothScrollTargetOffset;
    private DateTime _scriptureSmoothScrollLastFrame;
    private bool _scriptureSmoothScrollActive;
    private double _studyEditorSmoothScrollTargetOffset;
    private DateTime _studyEditorSmoothScrollLastFrame;
    private bool _studyEditorSmoothScrollActive;
    private double _todaySmoothScrollTargetOffset;
    private DateTime _todaySmoothScrollLastFrame;
    private bool _todaySmoothScrollActive;
    private double _scheduledMessagesSmoothScrollTargetOffset;
    private DateTime _scheduledMessagesSmoothScrollLastFrame;
    private bool _scheduledMessagesSmoothScrollActive;
    private double _strongsSmoothScrollTargetOffset;
    private DateTime _strongsSmoothScrollLastFrame;
    private bool _strongsSmoothScrollActive;
    private bool _maybeInSlashCommand;
    private bool _isApplyingEditorCommand;
    private bool _isPageTransitioning;
    private bool _isLoadingPassageSelection;
    private bool _isLoadingDailyDetails;
    private bool _isLoadingReminderSettings;
    private bool _isInitializing = true;
    private bool _sidebarCollapsed;
    private bool _scripturePanelHidden;
    private bool _strongsEnabled;
    private bool _greekLexiconLoaded;
    private bool _suppressRenameCommit;
    private GridLength _scripturePanelVisibleWidth = new(440);
    private GridLength _strongsPanelVisibleWidth = new(330);
    private MovablePanelKind? _draggingPanelKind;
    private TranslateTransform? _draggingPanelTransform;
    private UIElement? _draggingPanelHandle;
    private Point _panelDragStartPoint;
    private Point _panelDragStartOffset;
    private Point _panelDragPointer;
    private Point _panelDragPendingOffset;
    private bool _panelDragRenderSubscribed;
    private PanelPreset? _hoveredPanelPreset;
    private PanelPreset _scripturePanelPreset = PanelPreset.Right;
    private PanelPreset _strongsPanelPreset = PanelPreset.Right;

    public ObservableCollection<BibleBook> BibleBooks { get; } = new();
    public ObservableCollection<BibleBook> OldTestamentBooks { get; } = new();
    public ObservableCollection<BibleBook> NewTestamentBooks { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _accentPalette =
        [
            BrushFrom("#DDA15E"),
            BrushFrom("#BC6C25"),
            BrushFrom("#FEFAE0"),
            BrushFrom("#A98467"),
            BrushFrom("#606C38"),
            BrushFrom("#D6C99D"),
            BrushFrom("#F1E7C5"),
            BrushFrom("#4F5A2F")
        ];

        _slashCommands =
        [
            new EditorCommandOption("Text", "Plain paragraph", "T", "text normal paragraph", EditorBlockKind.Normal, PickEditorBrush("TextSecondary")),
            new EditorCommandOption("Heading 1", "Large section heading", "H1", "h1 heading title", EditorBlockKind.HeadingOne, PickEditorBrush("Gold")),
            new EditorCommandOption("Heading 2", "Subsection heading", "H2", "h2 heading subtitle", EditorBlockKind.HeadingTwo, PickEditorBrush("Sky")),
            new EditorCommandOption("Heading 3", "Small section heading", "H3", "h3 heading subtitle small", EditorBlockKind.HeadingThree, PickEditorBrush("Violet")),
            new EditorCommandOption("Quote", "Scripture, commentary, or cited text", "Q", "q quote quo citation blockquote", EditorBlockKind.Quote, PickEditorBrush("Mint")),
            new EditorCommandOption("Bulleted List", "Simple list item", "-", "bullet bulleted list unordered", EditorBlockKind.BulletedList, PickEditorBrush("Coral")),
            new EditorCommandOption("Numbered List", "Ordered list item", "1.", "number numbered list ordered", EditorBlockKind.NumberedList, PickEditorBrush("Gold")),
            new EditorCommandOption("To-do", "Checkbox-style action item", "[ ]", "todo to-do task checkbox check", EditorBlockKind.Todo, PickEditorBrush("Mint")),
            new EditorCommandOption("Callout", "Highlighted note block", "!", "callout note highlight reminder", EditorBlockKind.Callout, PickEditorBrush("Sky")),
            new EditorCommandOption("Code", "Monospace study snippet", "</>", "code mono monospace snippet", EditorBlockKind.Code, PickEditorBrush("Violet"))
        ];
        _slashCommandRefreshTimer = new DispatcherTimer(DispatcherPriority.ContextIdle)
        {
            Interval = TimeSpan.FromMilliseconds(12)
        };
        _slashCommandRefreshTimer.Tick += SlashCommandRefreshTimer_Tick;
        _workspaceSaveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(450)
        };
        _workspaceSaveTimer.Tick += WorkspaceSaveTimer_Tick;
        _toastTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2.4)
        };
        _toastTimer.Tick += ToastTimer_Tick;
        _workspaceFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Bible Study Studio",
            "workspace.json");
        LoadBibleBooks();
        LoadDefaultColorSchemes();
        OldTestamentBooksItems.ItemsSource = OldTestamentBooks;
        NewTestamentBooksItems.ItemsSource = NewTestamentBooks;
        SlashCommandList.ItemsSource = _filteredSlashCommands;
        TodayEditedItems.ItemsSource = _todayEditedStudies;
        ScheduledNotificationItems.ItemsSource = _scheduledNotificationDisplays;
        ReminderScheduleItems.ItemsSource = _reminderScheduleItems;
        ReminderPresetSelect.ItemsSource = _reminderPresets;
        ColorSchemePresetItems.ItemsSource = _colorSchemePresets;
        LoadWorkspaceState();
        LoadScriptureText();
        LoadTagntData();
        WorkspaceCountText.Text = $"{BibleBooks.Count} books ready";
        RenderBreadcrumbs();
        RenderTodayDashboard();
        SetActiveNavTab(AppNavTab.BibleStudy);
        _isInitializing = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        StopBibleBooksSmoothScroll();
        StopScriptureSmoothScroll();
        StopStudyEditorSmoothScroll();
        StopTodaySmoothScroll();
        StopScheduledMessagesSmoothScroll();
        StopStrongsSmoothScroll();
        StopPanelDragRendering();
        StopScrollBarRevealTimers();
        SaveWorkspaceState();
        base.OnClosing(e);
    }

    private void LoadBibleBooks()
    {
        AddOldTestamentBooks();
        AddNewTestamentBooks();
    }

    private void AddOldTestamentBooks()
    {
        AddBook("Genesis", "Old Testament", 50);
        AddBook("Exodus", "Old Testament", 40);
        AddBook("Leviticus", "Old Testament", 27);
        AddBook("Numbers", "Old Testament", 36);
        AddBook("Deuteronomy", "Old Testament", 34);
        AddBook("Joshua", "Old Testament", 24);
        AddBook("Judges", "Old Testament", 21);
        AddBook("Ruth", "Old Testament", 4);
        AddBook("1 Samuel", "Old Testament", 31);
        AddBook("2 Samuel", "Old Testament", 24);
        AddBook("1 Kings", "Old Testament", 22);
        AddBook("2 Kings", "Old Testament", 25);
        AddBook("1 Chronicles", "Old Testament", 29);
        AddBook("2 Chronicles", "Old Testament", 36);
        AddBook("Ezra", "Old Testament", 10);
        AddBook("Nehemiah", "Old Testament", 13);
        AddBook("Esther", "Old Testament", 10);
        AddBook("Job", "Old Testament", 42);
        AddBook("Psalms", "Old Testament", 150);
        AddBook("Proverbs", "Old Testament", 31);
        AddBook("Ecclesiastes", "Old Testament", 12);
        AddBook("Song of Solomon", "Old Testament", 8);
        AddBook("Isaiah", "Old Testament", 66);
        AddBook("Jeremiah", "Old Testament", 52);
        AddBook("Lamentations", "Old Testament", 5);
        AddBook("Ezekiel", "Old Testament", 48);
        AddBook("Daniel", "Old Testament", 12);
        AddBook("Hosea", "Old Testament", 14);
        AddBook("Joel", "Old Testament", 3);
        AddBook("Amos", "Old Testament", 9);
        AddBook("Obadiah", "Old Testament", 1);
        AddBook("Jonah", "Old Testament", 4);
        AddBook("Micah", "Old Testament", 7);
        AddBook("Nahum", "Old Testament", 3);
        AddBook("Habakkuk", "Old Testament", 3);
        AddBook("Zephaniah", "Old Testament", 3);
        AddBook("Haggai", "Old Testament", 2);
        AddBook("Zechariah", "Old Testament", 14);
        AddBook("Malachi", "Old Testament", 4);
    }

    private void AddNewTestamentBooks()
    {
        AddBook("Matthew", "New Testament", 28);
        AddBook("Mark", "New Testament", 16);
        AddBook("Luke", "New Testament", 24);
        AddBook("John", "New Testament", 21);
        AddBook("Acts", "New Testament", 28);
        AddBook("Romans", "New Testament", 16);
        AddBook("1 Corinthians", "New Testament", 16);
        AddBook("2 Corinthians", "New Testament", 13);
        AddBook("Galatians", "New Testament", 6);
        AddBook("Ephesians", "New Testament", 6);
        AddBook("Philippians", "New Testament", 4);
        AddBook("Colossians", "New Testament", 4);
        AddBook("1 Thessalonians", "New Testament", 5);
        AddBook("2 Thessalonians", "New Testament", 3);
        AddBook("1 Timothy", "New Testament", 6);
        AddBook("2 Timothy", "New Testament", 4);
        AddBook("Titus", "New Testament", 3);
        AddBook("Philemon", "New Testament", 1);
        AddBook("Hebrews", "New Testament", 13);
        AddBook("James", "New Testament", 5);
        AddBook("1 Peter", "New Testament", 5);
        AddBook("2 Peter", "New Testament", 3);
        AddBook("1 John", "New Testament", 5);
        AddBook("2 John", "New Testament", 1);
        AddBook("3 John", "New Testament", 1);
        AddBook("Jude", "New Testament", 1);
        AddBook("Revelation", "New Testament", 22);
    }

    private void AddBook(string name, string testament, int chapters)
    {
        if (!BibleVerseCounts.TryGetValue(name, out var verseCounts))
        {
            verseCounts = Enumerable.Repeat(1, chapters).ToArray();
        }

        BibleBooks.Add(new BibleBook(
            name,
            testament,
            verseCounts,
            _accentPalette[BibleBooks.Count % _accentPalette.Length]));
        var book = BibleBooks[^1];
        if (string.Equals(testament, "Old Testament", StringComparison.OrdinalIgnoreCase))
        {
            OldTestamentBooks.Add(book);
        }
        else
        {
            NewTestamentBooks.Add(book);
        }
    }

    private void LoadWorkspaceState()
    {
        if (!File.Exists(_workspaceFilePath))
        {
            EnsureDefaultReminderSettings();
            LoadColorSettings(null);
            return;
        }

        try
        {
            var json = File.ReadAllText(_workspaceFilePath);
            var state = JsonSerializer.Deserialize<WorkspaceState>(json);
            if (state is null)
            {
                return;
            }

            _workspaceByBook.Clear();
            _dailyNotesByDate.Clear();
            _scheduledNotifications.Clear();
            foreach (var bookState in state.Books)
            {
                var book = BibleBooks.FirstOrDefault(candidate => candidate.Name == bookState.Name);
                var fallbackBrush = book?.AccentBrush ?? PickEditorBrush("Mint");
                var workspace = CreateWorkspaceItemFromState(bookState, null, fallbackBrush);
                _workspaceByBook[workspace.Name] = workspace;
            }

            foreach (var dayState in state.DailyNotes)
            {
                if (!string.IsNullOrWhiteSpace(dayState.DateKey))
                {
                    _dailyNotesByDate[dayState.DateKey] = dayState;
                }
            }

            _scheduledNotifications.AddRange(state.ScheduledNotifications
                .Where(notification => !string.IsNullOrWhiteSpace(notification.SequenceId)));

            OldTestamentBooksExpander.IsExpanded = state.OldTestamentBooksExpanded;
            NewTestamentBooksExpander.IsExpanded = state.NewTestamentBooksExpanded;
            LoadReminderSettings(state.ReminderSettings);
            LoadColorSettings(state.ColorSettings);
        }
        catch
        {
            _workspaceByBook.Clear();
            _dailyNotesByDate.Clear();
            _scheduledNotifications.Clear();
            _reminderScheduleItems.Clear();
            _reminderPresets.Clear();
            EnsureDefaultReminderSettings();
            LoadDefaultColorSchemes();
            LoadColorSettings(null);
        }
    }

    private WorkspaceItem CreateWorkspaceItemFromState(WorkspaceItemState state, WorkspaceItem? parent, Brush fallbackBrush)
    {
        var accentBrush = string.IsNullOrWhiteSpace(state.AccentHex)
            ? fallbackBrush
            : BrushFrom(state.AccentHex);
        var item = new WorkspaceItem(state.Name, state.Kind, accentBrush, parent);
        item.PassageStartChapter = state.PassageStartChapter;
        item.PassageStartVerse = state.PassageStartVerse;
        item.PassageEndChapter = state.PassageEndChapter;
        item.PassageEndVerse = state.PassageEndVerse;
        item.LastEditedAt = state.LastEditedAt;

        foreach (var blockState in state.Blocks)
        {
            item.Blocks.Add(CreateStudyBlockFromState(blockState));
        }

        foreach (var childState in state.Children)
        {
            item.Children.Add(CreateWorkspaceItemFromState(childState, item, accentBrush));
        }

        return item;
    }

    private StudyBlock CreateStudyBlockFromState(StudyBlockState state)
    {
        var block = CreateStudyBlock(state.Kind);
        block.Text = state.Text;
        block.IsChecked = state.IsChecked;
        return block;
    }

    private void WorkspaceSaveTimer_Tick(object? sender, EventArgs e)
    {
        _workspaceSaveTimer.Stop();
        SaveWorkspaceState();
    }

    private void QueueWorkspaceSave()
    {
        if (_workspaceSaveTimer is null)
        {
            return;
        }

        _workspaceSaveTimer.Stop();
        _workspaceSaveTimer.Start();
    }

    private void ShowToast(string message)
    {
        if (_isInitializing || ToastHost is null || ToastText is null || ToastHostTransform is null)
        {
            return;
        }

        ToastText.Text = message;
        ToastHost.Visibility = Visibility.Visible;
        ToastHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        ToastHostTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(28, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        ToastHostTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-8, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void ToastTimer_Tick(object? sender, EventArgs e)
    {
        _toastTimer.Stop();
        HideToast();
    }

    private void HideToast()
    {
        if (ToastHost is null || ToastHostTransform is null)
        {
            return;
        }

        var fade = new DoubleAnimation(ToastHost.Opacity, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) => ToastHost.Visibility = Visibility.Collapsed;
        ToastHost.BeginAnimation(OpacityProperty, fade);
        ToastHostTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, 22, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        });
    }

    private void SaveWorkspaceState()
    {
        if (_workspaceSaveTimer is null)
        {
            return;
        }

        _workspaceSaveTimer.Stop();

        try
        {
            var directory = Path.GetDirectoryName(_workspaceFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new WorkspaceState
            {
                Books = _workspaceByBook.Values
                    .Select(CreateWorkspaceItemState)
                    .ToList(),
                DailyNotes = _dailyNotesByDate.Values
                    .OrderByDescending(day => day.DateKey)
                    .ToList(),
                ScheduledNotifications = _scheduledNotifications
                    .OrderByDescending(notification => notification.CreatedAt)
                    .ToList(),
                OldTestamentBooksExpanded = OldTestamentBooksExpander.IsExpanded,
                NewTestamentBooksExpanded = NewTestamentBooksExpander.IsExpanded,
                ReminderSettings = CreateReminderSettingsState(),
                ColorSettings = CreateColorSettingsState()
            };
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            var tempPath = $"{_workspaceFilePath}.tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _workspaceFilePath, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Workspace save failed: {ex}");
        }
    }

    private void TestamentBooksExpander_StateChanged(object sender, RoutedEventArgs e)
    {
        if (sender is Expander expander)
        {
            AnimateTestamentExpander(expander, expander.IsExpanded);
        }

        if (_isInitializing)
        {
            return;
        }

        QueueWorkspaceSave();
    }

    private void TestamentBooksExpander_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Expander expander)
        {
            return;
        }

        SetTestamentExpanderState(expander, expander.IsExpanded);
    }

    private void SetTestamentExpanderState(Expander expander, bool isExpanded)
    {
        if (expander.Template.FindName("ExpandSite", expander) is not Border site
            || expander.Template.FindName("ExpandSiteScale", expander) is not ScaleTransform scale)
        {
            return;
        }

        site.Opacity = isExpanded ? 1 : 0;
        scale.ScaleY = isExpanded ? 1 : 0;
    }

    private void AnimateTestamentExpander(Expander expander, bool isExpanded)
    {
        if (expander.Template.FindName("ExpandSite", expander) is not Border site
            || expander.Template.FindName("ExpandSiteScale", expander) is not ScaleTransform scale)
        {
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var duration = TimeSpan.FromMilliseconds(240);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(
            scale.ScaleY,
            isExpanded ? 1 : 0,
            duration)
        {
            EasingFunction = ease
        });
        site.BeginAnimation(OpacityProperty, new DoubleAnimation(
            site.Opacity,
            isExpanded ? 1 : 0,
            TimeSpan.FromMilliseconds(isExpanded ? 200 : 150))
        {
            EasingFunction = ease
        });
    }

    private WorkspaceItemState CreateWorkspaceItemState(WorkspaceItem item)
    {
        return new WorkspaceItemState
        {
            Name = item.Name,
            Kind = item.Kind,
            AccentHex = BrushToHex(item.AccentBrush),
            PassageStartChapter = item.PassageStartChapter,
            PassageStartVerse = item.PassageStartVerse,
            PassageEndChapter = item.PassageEndChapter,
            PassageEndVerse = item.PassageEndVerse,
            LastEditedAt = item.LastEditedAt,
            Children = item.Children
                .Select(CreateWorkspaceItemState)
                .ToList(),
            Blocks = item.Blocks
                .Select(CreateStudyBlockState)
                .ToList()
        };
    }

    private static StudyBlockState CreateStudyBlockState(StudyBlock block)
    {
        return new StudyBlockState
        {
            Kind = block.Kind,
            Text = block.Text,
            IsChecked = block.IsChecked
        };
    }

    private void LoadReminderSettings(ReminderSettingsState? settings)
    {
        _isLoadingReminderSettings = true;
        _reminderScheduleItems.Clear();
        _reminderPresets.Clear();

        var source = settings ?? CreateDefaultReminderSettingsState();
        OverviewNotificationEnabledCheckBox.IsChecked = source.OverviewEnabled;
        OverviewPromptTextBox.Text = string.IsNullOrWhiteSpace(source.OverviewPrompt)
            ? DefaultOverviewPrompt
            : source.OverviewPrompt.Trim();
        RemindersEnabledCheckBox.IsChecked = source.RemindersEnabled;

        foreach (var reminder in source.Reminders)
        {
            AddReminderScheduleItem(CreateReminderScheduleItem(reminder));
        }

        foreach (var preset in source.Presets)
        {
            _reminderPresets.Add(new ReminderPreset(
                preset.Name,
                preset.Reminders.Select(CreateReminderScheduleItem).ToList()));
        }

        if (_reminderScheduleItems.Count == 0)
        {
            foreach (var reminder in CreateDefaultReminderSettingsState().Reminders)
            {
                AddReminderScheduleItem(CreateReminderScheduleItem(reminder));
            }
        }

        if (_reminderPresets.Count == 0)
        {
            _reminderPresets.Add(CreateBuiltInReminderPreset());
        }

        ReminderPresetSelect.SelectedIndex = _reminderPresets.Count > 0 ? 0 : -1;
        UpdateReminderCount();
        _isLoadingReminderSettings = false;
    }

    private void EnsureDefaultReminderSettings()
    {
        LoadReminderSettings(CreateDefaultReminderSettingsState());
    }

    private ReminderSettingsState CreateReminderSettingsState()
    {
        return new ReminderSettingsState
        {
            OverviewEnabled = OverviewNotificationEnabledCheckBox.IsChecked == true,
            OverviewPrompt = string.IsNullOrWhiteSpace(OverviewPromptTextBox.Text)
                ? DefaultOverviewPrompt
                : OverviewPromptTextBox.Text.Trim(),
            RemindersEnabled = RemindersEnabledCheckBox.IsChecked == true,
            Reminders = _reminderScheduleItems
                .Select(CreateReminderScheduleItemState)
                .ToList(),
            Presets = _reminderPresets
                .Select(preset => new ReminderPresetState
                {
                    Name = preset.Name,
                    Reminders = preset.Reminders
                        .Select(CreateReminderScheduleItemState)
                        .ToList()
                })
                .ToList()
        };
    }

    private static ReminderSettingsState CreateDefaultReminderSettingsState()
    {
        return new ReminderSettingsState
        {
            OverviewEnabled = true,
            OverviewPrompt = DefaultOverviewPrompt,
            RemindersEnabled = true,
            Reminders =
            [
                new ReminderScheduleItemState { DelayAmount = 2, DelayUnit = "hours", Prompt = "Help me pause and reconnect with the passage." },
                new ReminderScheduleItemState { DelayAmount = 5, DelayUnit = "hours", Prompt = "Encourage me to practice one truth from this study before evening." },
                new ReminderScheduleItemState { DelayAmount = 9, DelayUnit = "hours", Prompt = "Help me review what stood out and end the day with one reflection." }
            ],
            Presets =
            [
                new ReminderPresetState
                {
                    Name = "Classic Study Day",
                    Reminders =
                    [
                        new ReminderScheduleItemState { DelayAmount = 2, DelayUnit = "hours", Prompt = "Help me pause and reconnect with the passage." },
                        new ReminderScheduleItemState { DelayAmount = 5, DelayUnit = "hours", Prompt = "Encourage me to practice one truth from this study before evening." },
                        new ReminderScheduleItemState { DelayAmount = 9, DelayUnit = "hours", Prompt = "Help me review what stood out and end the day with one reflection." }
                    ]
                }
            ]
        };
    }

    private void LoadDefaultColorSchemes()
    {
        _colorSchemePresets.Clear();
        _colorSchemePresets.Add(new ColorSchemePreset("Olive Study", "#283618", "#1F2A14", "#606C38", "#DDA15E", "#FEFAE0", "#F1E7C5", true));
        _colorSchemePresets.Add(new ColorSchemePreset("Forest Light", "#1E2B20", "#172318", "#4E7042", "#B8D58A", "#F3F8E8", "#DDE8C7", true));
        _colorSchemePresets.Add(new ColorSchemePreset("Deep Walnut", "#2B2118", "#1F1712", "#694B35", "#D9A05F", "#FFF4DF", "#E7D2B5", true));
    }

    private void LoadColorSettings(ColorSettingsState? settings)
    {
        if (settings is not null)
        {
            LoadDefaultColorSchemes();
            foreach (var preset in settings.Presets.Where(preset => !preset.BuiltIn))
            {
                _colorSchemePresets.Add(preset);
            }
        }

        var selected = _colorSchemePresets.FirstOrDefault(preset =>
                string.Equals(preset.Name, settings?.SelectedSchemeName, StringComparison.OrdinalIgnoreCase))
            ?? _colorSchemePresets.FirstOrDefault();
        if (selected is null)
        {
            return;
        }

        ApplyColorScheme(selected);
    }

    private ColorSettingsState CreateColorSettingsState()
    {
        return new ColorSettingsState
        {
            SelectedSchemeName = _selectedColorSchemeName,
            Presets = _colorSchemePresets.ToList()
        };
    }

    private void ApplyColorScheme(ColorSchemePreset preset)
    {
        _selectedColorSchemeName = preset.Name;
        SetBrushColor("AppBackground", preset.AppBackground);
        SetBrushColor("SidebarBackground", preset.SidebarBackground);
        SetBrushColor("PanelBackground", preset.PanelBackground);
        SetBrushColor("Mint", preset.Accent);
        SetBrushColor("Gold", preset.Accent);
        SetBrushColor("TextPrimary", preset.TextPrimary);
        SetBrushColor("Sky", preset.TextPrimary);
        SetBrushColor("TextSecondary", preset.TextSecondary);
        SetBrushColor("TextMuted", preset.TextSecondary);
        SetBrushColor("StrokeSoft", preset.AppBackground);
        SetBrushColor("Coral", preset.Accent);
        RefreshStudyBlockThemeBrushes();
    }

    private void SetBrushColor(string resourceName, string hex)
    {
        if (TryParseColor(hex, out var color))
        {
            Resources[resourceName] = new SolidColorBrush(color);
        }
    }

    private static bool TryParseColor(string value, out Color color)
    {
        try
        {
            color = (Color)ColorConverter.ConvertFromString(value.Trim());
            return true;
        }
        catch
        {
            color = Colors.Transparent;
            return false;
        }
    }

    private ReminderPreset CreateBuiltInReminderPreset()
    {
        return new ReminderPreset(
            "Classic Study Day",
            CreateDefaultReminderSettingsState().Reminders.Select(CreateReminderScheduleItem).ToList());
    }

    private ReminderScheduleItem CreateReminderScheduleItem(ReminderScheduleItemState state)
    {
        var item = new ReminderScheduleItem(
            Math.Max(1, state.DelayAmount),
            NormalizeReminderUnit(state.DelayUnit),
            string.IsNullOrWhiteSpace(state.Prompt)
                ? "Return to {passage}."
                : state.Prompt.Trim());
        item.PropertyChanged += ReminderScheduleItem_PropertyChanged;
        return item;
    }

    private void AddReminderScheduleItem(ReminderScheduleItem item, bool insertFirst = false)
    {
        item.PropertyChanged -= ReminderScheduleItem_PropertyChanged;
        item.PropertyChanged += ReminderScheduleItem_PropertyChanged;
        if (insertFirst)
        {
            _reminderScheduleItems.Insert(0, item);
        }
        else
        {
            _reminderScheduleItems.Add(item);
        }

        UpdateReminderCount();
    }

    private static ReminderScheduleItemState CreateReminderScheduleItemState(ReminderScheduleItem item)
    {
        return new ReminderScheduleItemState
        {
            DelayAmount = Math.Max(1, item.DelayAmount),
            DelayUnit = NormalizeReminderUnit(item.DelayUnit),
            Prompt = item.Prompt.Trim()
        };
    }

    private static string NormalizeReminderUnit(string? unit)
    {
        return unit?.Trim().ToLowerInvariant() switch
        {
            "minute" or "minutes" => "minutes",
            "day" or "days" => "days",
            _ => "hours"
        };
    }

    private void ReminderScheduleItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingReminderSettings)
        {
            return;
        }

        QueueWorkspaceSave();
    }

    private static string BrushToHex(Brush brush)
    {
        return brush is SolidColorBrush solidColorBrush
            ? solidColorBrush.Color.ToString()
            : "#FFFFFFFF";
    }

    private void RunPageTransition(object? source, Action switchPage)
    {
        if (_isPageTransitioning || MainContentRoot is null || PageTransitionOverlay is null || PageTransitionCircle is null)
        {
            switchPage();
            return;
        }

        _isPageTransitioning = true;

        var origin = new Point(MainContentRoot.ActualWidth / 2, MainContentRoot.ActualHeight / 2);
        if (source is FrameworkElement element)
        {
            try
            {
                origin = element.TranslatePoint(new Point(element.ActualWidth / 2, element.ActualHeight / 2), MainContentRoot);
            }
            catch
            {
                origin = new Point(MainContentRoot.ActualWidth / 2, MainContentRoot.ActualHeight / 2);
            }
        }

        var maxX = Math.Max(Math.Abs(origin.X), Math.Abs(MainContentRoot.ActualWidth - origin.X));
        var maxY = Math.Max(Math.Abs(origin.Y), Math.Abs(MainContentRoot.ActualHeight - origin.Y));
        var finalDiameter = Math.Sqrt((maxX * maxX) + (maxY * maxY)) * 2.35;

        PageTransitionOverlay.Visibility = Visibility.Visible;
        PageTransitionOverlay.Opacity = 1;
        PageTransitionCircle.Width = 0;
        PageTransitionCircle.Height = 0;
        Canvas.SetLeft(PageTransitionCircle, origin.X);
        Canvas.SetTop(PageTransitionCircle, origin.Y);

        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var growDuration = TimeSpan.FromMilliseconds(200);
        var growWidth = new DoubleAnimation(0, finalDiameter, growDuration) { EasingFunction = ease };
        var growHeight = new DoubleAnimation(0, finalDiameter, growDuration) { EasingFunction = ease };
        var moveLeft = new DoubleAnimation(origin.X, origin.X - finalDiameter / 2, growDuration) { EasingFunction = ease };
        var moveTop = new DoubleAnimation(origin.Y, origin.Y - finalDiameter / 2, growDuration) { EasingFunction = ease };

        growWidth.Completed += (_, _) =>
        {
            switchPage();

            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(100))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            fade.Completed += (_, _) =>
            {
                PageTransitionOverlay.Visibility = Visibility.Collapsed;
                PageTransitionOverlay.Opacity = 1;
                PageTransitionCircle.BeginAnimation(WidthProperty, null);
                PageTransitionCircle.BeginAnimation(HeightProperty, null);
                PageTransitionOverlay.BeginAnimation(OpacityProperty, null);
                _isPageTransitioning = false;
            };
            PageTransitionOverlay.BeginAnimation(OpacityProperty, fade);
        };

        PageTransitionCircle.BeginAnimation(WidthProperty, growWidth);
        PageTransitionCircle.BeginAnimation(HeightProperty, growHeight);
        PageTransitionCircle.BeginAnimation(Canvas.LeftProperty, moveLeft);
        PageTransitionCircle.BeginAnimation(Canvas.TopProperty, moveTop);
    }

    private void BibleBook_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: BibleBook book })
        {
            return;
        }

        RunPageTransition(sender, () => OpenBook(book));
    }

    private void BibleStudyNav_Click(object sender, RoutedEventArgs e)
    {
        RunPageTransition(sender, NavigateBibleStudyNav);
    }

    private void NavigateBibleStudyNav()
    {
        if (_selectedBook is null)
        {
            BackToLibrary();
            return;
        }

        _currentStudy = null;
        LibraryPanel.Visibility = Visibility.Collapsed;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        WorkspacePanel.Visibility = Visibility.Visible;
        StudyPanel.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.BibleStudy);
        RenderWorkspace();
        RenderBreadcrumbs();
    }

    private void TodayDashboard_Click(object sender, RoutedEventArgs e)
    {
        RunPageTransition(sender, ShowTodayDashboard);
    }

    private void OpenBook(BibleBook book)
    {
        _selectedBook = book;
        _currentStudy = null;
        _currentContainer = GetBookWorkspace(book);

        LibraryPanel.Visibility = Visibility.Collapsed;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        StudyPanel.Visibility = Visibility.Collapsed;
        WorkspacePanel.Visibility = Visibility.Visible;
        RestoreContentShell();
        HeaderBackButton.Visibility = Visibility.Collapsed;
        WorkspaceStatusCard.Visibility = Visibility.Visible;
        PassagePickerCard.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.BibleStudy);

        RenderWorkspace();
        RenderBreadcrumbs();
    }

    private WorkspaceItem GetBookWorkspace(BibleBook book)
    {
        if (_workspaceByBook.TryGetValue(book.Name, out var workspace))
        {
            return workspace;
        }

        workspace = new WorkspaceItem(book.Name, WorkspaceItemKind.Book, book.AccentBrush, null);
        _workspaceByBook[book.Name] = workspace;
        return workspace;
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        AddWorkspaceItem(WorkspaceItemKind.Folder);
    }

    private void AddStudy_Click(object sender, RoutedEventArgs e)
    {
        AddWorkspaceItem(WorkspaceItemKind.Study);
    }

    private void AddWorkspaceItem(WorkspaceItemKind kind)
    {
        if (_currentContainer is null || _selectedBook is null)
        {
            return;
        }

        var baseName = kind == WorkspaceItemKind.Folder ? "New Folder" : "New Study";
        var item = new WorkspaceItem(
            CreateUniqueName(baseName, _currentContainer.Children),
            kind,
            PickAccentBrush(_currentContainer.Children.Count),
            _currentContainer);

        _currentContainer.Children.Add(item);
        _currentContainer.RefreshDetail();
        QueueWorkspaceSave();
        RenderWorkspace();
        BeginWorkspaceItemRename(item);
        ShowToast(kind == WorkspaceItemKind.Folder
            ? "Folder created"
            : "Study created");
    }

    private void WorkspaceItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: WorkspaceItem item })
        {
            return;
        }

        RunPageTransition(sender, () => OpenWorkspaceItem(item));
    }

    private void OpenWorkspaceItem(WorkspaceItem item)
    {
        if (item.Kind == WorkspaceItemKind.Folder)
        {
            _currentContainer = item;
            _currentStudy = null;
            TodayPanel.Visibility = Visibility.Collapsed;
            ReminderSettingsPanel.Visibility = Visibility.Collapsed;
            SetSettingsPanelVisibility(Visibility.Collapsed);
            WorkspacePanel.Visibility = Visibility.Visible;
            StudyPanel.Visibility = Visibility.Collapsed;
            RenderWorkspace();
        }
        else if (item.Kind == WorkspaceItemKind.Study)
        {
            OpenStudy(item);
        }

        RenderBreadcrumbs();
    }

    private void BeginWorkspaceItemRename(WorkspaceItem item)
    {
        if (item.Kind == WorkspaceItemKind.Book)
        {
            return;
        }

        item.BeginRename();
        Dispatcher.BeginInvoke(() =>
        {
            var textBox = FindWorkspaceRenameTextBox(item);
            if (textBox is null)
            {
                return;
            }

            textBox.Focus();
            textBox.SelectAll();
        }, DispatcherPriority.Background);
    }

    private void WorkspaceRenameTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { Tag: WorkspaceItem { IsRenaming: true } } textBox)
        {
            Dispatcher.BeginInvoke(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
            }, DispatcherPriority.Background);
        }
    }

    private void WorkspaceRenameTextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is TextBox textBox && !textBox.IsKeyboardFocusWithin)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    private void WorkspaceRenameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { Tag: WorkspaceItem item } textBox)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            _suppressRenameCommit = true;
            CommitWorkspaceItemRename(item, textBox.Text);
            Keyboard.ClearFocus();
            _suppressRenameCommit = false;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            _suppressRenameCommit = true;
            item.CancelRename();
            Keyboard.ClearFocus();
            _suppressRenameCommit = false;
            e.Handled = true;
        }
    }

    private void WorkspaceRenameTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_suppressRenameCommit || sender is not TextBox { Tag: WorkspaceItem item })
        {
            return;
        }

        CommitWorkspaceItemRename(item, item.Name);
    }

    private void CommitWorkspaceItemRename(WorkspaceItem item, string requestedName)
    {
        var parent = item.Parent;
        var fallbackName = string.IsNullOrWhiteSpace(item.RenameOriginalName)
            ? (item.Kind == WorkspaceItemKind.Folder ? "New Folder" : "New Study")
            : item.RenameOriginalName;
        var trimmedName = string.IsNullOrWhiteSpace(requestedName)
            ? fallbackName
            : requestedName.Trim();

        if (parent is not null)
        {
            trimmedName = CreateUniqueName(trimmedName, parent.Children.Where(sibling => !ReferenceEquals(sibling, item)));
        }

        item.Name = trimmedName;
        item.EndRename();
        parent?.RefreshDetail();
        if (ReferenceEquals(_currentStudy, item))
        {
            MainHeading.Text = item.Name;
            MainSubheading.Visibility = Visibility.Collapsed;
        }

        RenderBreadcrumbs();
        QueueWorkspaceSave();
        ShowToast("Name saved");
    }

    private void BackOneLevel_Click(object sender, RoutedEventArgs e)
    {
        RunPageTransition(sender, BackOneLevel);
    }

    private void BackOneLevel()
    {
        if (IsColorThemeSettingsPanelVisible() || ReminderSettingsPanel.Visibility == Visibility.Visible)
        {
            ShowSettings();
            return;
        }

        if (_currentStudy is not null)
        {
            _currentStudy = null;
            TodayPanel.Visibility = Visibility.Collapsed;
            ReminderSettingsPanel.Visibility = Visibility.Collapsed;
            SetSettingsPanelVisibility(Visibility.Collapsed);
            WorkspacePanel.Visibility = Visibility.Visible;
            StudyPanel.Visibility = Visibility.Collapsed;
            RestoreContentShell();
            HeaderBackButton.Visibility = Visibility.Collapsed;
            WorkspaceStatusCard.Visibility = Visibility.Visible;
            PassagePickerCard.Visibility = Visibility.Collapsed;
            RenderWorkspace();
            RenderBreadcrumbs();
            return;
        }

        if (_currentContainer?.Parent is not null)
        {
            _currentContainer = _currentContainer.Parent;
            RenderWorkspace();
            RenderBreadcrumbs();
            return;
        }

        BackToLibrary();
    }

    private void BackToLibrary()
    {
        _selectedBook = null;
        _currentContainer = null;
        _currentStudy = null;

        LibraryPanel.Visibility = Visibility.Visible;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        WorkspacePanel.Visibility = Visibility.Collapsed;
        StudyPanel.Visibility = Visibility.Collapsed;
        MainHeading.Text = "Bible Study";
        MainSubheading.Visibility = Visibility.Collapsed;
        RestoreContentShell();
        HeaderBackButton.Visibility = Visibility.Collapsed;
        WorkspaceStatusCard.Visibility = Visibility.Visible;
        PassagePickerCard.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.BibleStudy);
        RenderBreadcrumbs();
    }

    private void ShowTodayDashboard()
    {
        _currentStudy = null;
        LibraryPanel.Visibility = Visibility.Collapsed;
        WorkspacePanel.Visibility = Visibility.Collapsed;
        StudyPanel.Visibility = Visibility.Collapsed;
        TodayPanel.Visibility = Visibility.Visible;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        MainHeading.Text = "Today";
        MainSubheading.Text = DateTime.Now.ToString("dddd, MMMM d, yyyy");
        MainSubheading.Visibility = Visibility.Visible;
        FlattenContentShell();
        HeaderBackButton.Visibility = Visibility.Collapsed;
        WorkspaceStatusCard.Visibility = Visibility.Collapsed;
        PassagePickerCard.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.Today);
        RenderTodayDashboard();
        RenderBreadcrumbs();
    }

    private void ReminderSettings_Click(object sender, RoutedEventArgs e)
    {
        RunPageTransition(sender, ShowReminderSettings);
    }

    private void ShowReminderSettings()
    {
        _currentStudy = null;
        LibraryPanel.Visibility = Visibility.Collapsed;
        WorkspacePanel.Visibility = Visibility.Collapsed;
        StudyPanel.Visibility = Visibility.Collapsed;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Visible;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        MainHeading.Text = "Reminder Settings";
        MainSubheading.Visibility = Visibility.Collapsed;
        FlattenContentShell();
        HeaderBackButton.Visibility = Visibility.Visible;
        WorkspaceStatusCard.Visibility = Visibility.Collapsed;
        PassagePickerCard.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.Settings);
        RenderBreadcrumbs();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        RunPageTransition(sender, ShowSettings);
    }

    private void ColorThemeSettings_Click(object sender, RoutedEventArgs e)
    {
        RunPageTransition(sender, ShowColorThemeSettings);
    }

    private void ShowSettings()
    {
        _currentStudy = null;
        LibraryPanel.Visibility = Visibility.Collapsed;
        WorkspacePanel.Visibility = Visibility.Collapsed;
        StudyPanel.Visibility = Visibility.Collapsed;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Visible);
        SetColorThemeSettingsPanelVisibility(Visibility.Collapsed);
        MainHeading.Text = "Settings";
        MainSubheading.Visibility = Visibility.Collapsed;
        FlattenContentShell();
        HeaderBackButton.Visibility = Visibility.Collapsed;
        WorkspaceStatusCard.Visibility = Visibility.Collapsed;
        PassagePickerCard.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.Settings);
        RenderBreadcrumbs();
    }

    private void ShowColorThemeSettings()
    {
        _currentStudy = null;
        LibraryPanel.Visibility = Visibility.Collapsed;
        WorkspacePanel.Visibility = Visibility.Collapsed;
        StudyPanel.Visibility = Visibility.Collapsed;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        SetColorThemeSettingsPanelVisibility(Visibility.Visible);
        MainHeading.Text = "Color Theme";
        MainSubheading.Visibility = Visibility.Collapsed;
        FlattenContentShell();
        HeaderBackButton.Visibility = Visibility.Visible;
        WorkspaceStatusCard.Visibility = Visibility.Collapsed;
        PassagePickerCard.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.Settings);
        RenderBreadcrumbs();
    }

    private void RenderWorkspace()
    {
        if (_currentContainer is null || _selectedBook is null)
        {
            return;
        }

        WorkspaceTitle.Text = _currentContainer.Name;
        WorkspaceSubtitle.Text = _currentContainer.Kind == WorkspaceItemKind.Book
            ? $"{_selectedBook.ChapterLabel} in the {_selectedBook.Testament}"
            : "Folders and studies in this folder";
        WorkspaceItems.ItemsSource = _currentContainer.Children;
        EmptyWorkspaceText.Visibility = _currentContainer.Children.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        MainHeading.Text = _selectedBook.Name;
        MainSubheading.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        RestoreContentShell();
        HeaderBackButton.Visibility = Visibility.Collapsed;
        WorkspaceStatusCard.Visibility = Visibility.Visible;
        PassagePickerCard.Visibility = Visibility.Collapsed;
    }

    private void RenderBreadcrumbs()
    {
        BreadcrumbTrail.Children.Clear();
        AddBreadcrumb("Bible Study", BreadcrumbTarget.Library, null, true);

        if (TodayPanel.Visibility == Visibility.Visible)
        {
            AddBreadcrumb("Today", BreadcrumbTarget.Today, null, false);
            return;
        }

        if (ReminderSettingsPanel.Visibility == Visibility.Visible)
        {
            AddBreadcrumb("Settings", BreadcrumbTarget.Settings, null, false);
            AddBreadcrumb("Reminder Settings", BreadcrumbTarget.ReminderSettings, null, false);
            return;
        }

        if (IsColorThemeSettingsPanelVisible())
        {
            AddBreadcrumb("Settings", BreadcrumbTarget.Settings, null, false);
            AddBreadcrumb("Color Theme", BreadcrumbTarget.ColorThemeSettings, null, false);
            return;
        }

        if (IsSettingsPanelVisible())
        {
            AddBreadcrumb("Settings", BreadcrumbTarget.Settings, null, false);
            return;
        }

        if (_selectedBook is null || _currentContainer is null)
        {
            return;
        }

        var lineage = GetContainerLineage(_currentContainer);
        foreach (var node in lineage)
        {
            AddBreadcrumb(node.Name, BreadcrumbTarget.WorkspaceItem, node, false);
        }

        if (_currentStudy is not null)
        {
            AddBreadcrumb(_currentStudy.Name, BreadcrumbTarget.Study, _currentStudy, false);
        }
    }

    private void SetSettingsPanelVisibility(Visibility visibility)
    {
        if (SettingsPanel is not null)
        {
            SettingsPanel.Visibility = visibility;
        }

        if (visibility == Visibility.Collapsed)
        {
            SetColorThemeSettingsPanelVisibility(Visibility.Collapsed);
        }
    }

    private bool IsSettingsPanelVisible()
    {
        return SettingsPanel is not null && SettingsPanel.Visibility == Visibility.Visible;
    }

    private void SetColorThemeSettingsPanelVisibility(Visibility visibility)
    {
        if (ColorThemeSettingsPanel is not null)
        {
            ColorThemeSettingsPanel.Visibility = visibility;
        }
    }

    private bool IsColorThemeSettingsPanelVisible()
    {
        return ColorThemeSettingsPanel is not null && ColorThemeSettingsPanel.Visibility == Visibility.Visible;
    }

    private void AddBreadcrumb(string text, BreadcrumbTarget target, object? tag, bool isFirst)
    {
        if (BreadcrumbTrail is null)
        {
            return;
        }

        if (!isFirst)
        {
            BreadcrumbTrail.Children.Add(new TextBlock
            {
                Text = " / ",
                Foreground = GetResourceBrush("TextMuted"),
                FontWeight = FontWeights.SemiBold
            });
        }

        var button = new Button();
        button.Content = text;
        button.Tag = new BreadcrumbPayload(target, tag);
        button.Background = Brushes.Transparent;
        button.BorderThickness = new Thickness(0);
        button.Padding = new Thickness(0);
        button.Cursor = Cursors.Hand;
        button.FontWeight = FontWeights.SemiBold;
        button.Foreground = target == BreadcrumbTarget.Library
            ? GetResourceBrush("Mint")
            : GetResourceBrush("TextSecondary");
        button.Click += Breadcrumb_Click;
        BreadcrumbTrail.Children.Add(button);
    }

    private void Breadcrumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: BreadcrumbPayload payload })
        {
            return;
        }

        RunPageTransition(sender, () => NavigateBreadcrumb(payload));
    }

    private void NavigateBreadcrumb(BreadcrumbPayload payload)
    {
        switch (payload.Target)
        {
            case BreadcrumbTarget.Library:
                BackToLibrary();
                break;
            case BreadcrumbTarget.WorkspaceItem when payload.Value is WorkspaceItem item:
                _currentContainer = item;
                _currentStudy = null;
                LibraryPanel.Visibility = Visibility.Collapsed;
                TodayPanel.Visibility = Visibility.Collapsed;
                ReminderSettingsPanel.Visibility = Visibility.Collapsed;
                SetSettingsPanelVisibility(Visibility.Collapsed);
                WorkspacePanel.Visibility = Visibility.Visible;
                StudyPanel.Visibility = Visibility.Collapsed;
                RestoreContentShell();
                HeaderBackButton.Visibility = Visibility.Collapsed;
                WorkspaceStatusCard.Visibility = Visibility.Visible;
                PassagePickerCard.Visibility = Visibility.Collapsed;
                RenderWorkspace();
                RenderBreadcrumbs();
                break;
            case BreadcrumbTarget.Study when payload.Value is WorkspaceItem study:
                OpenStudy(study);
                RenderBreadcrumbs();
                break;
            case BreadcrumbTarget.Today:
                ShowTodayDashboard();
                break;
            case BreadcrumbTarget.ReminderSettings:
                ShowReminderSettings();
                break;
            case BreadcrumbTarget.Settings:
                ShowSettings();
                break;
            case BreadcrumbTarget.ColorThemeSettings:
                ShowColorThemeSettings();
                break;
        }
    }

    private void OpenStudy(WorkspaceItem study)
    {
        _currentStudy = study;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        WorkspacePanel.Visibility = Visibility.Collapsed;
        StudyPanel.Visibility = Visibility.Visible;
        MainHeading.Text = study.Name;
        MainSubheading.Visibility = Visibility.Collapsed;
        FlattenContentShell();
        HeaderBackButton.Visibility = Visibility.Visible;
        WorkspaceStatusCard.Visibility = Visibility.Collapsed;
        PassagePickerCard.Visibility = Visibility.Visible;
        EnsureStudyHasBlocks(study);
        UpdateNumberedListLabels();
        ConfigurePassageSelectors(study);
        StudyBlockItems.ItemsSource = study.Blocks;
        HideSlashCommandMenu();
        SetActiveNavTab(AppNavTab.BibleStudy);
        FocusBlock(study.Blocks[0]);
    }

    private void SetActiveNavTab(AppNavTab activeTab)
    {
        _activeNavTab = activeTab;
        BibleStudyNavButton.Style = (Style)FindResource(activeTab == AppNavTab.BibleStudy
            ? "ActiveNavButtonStyle"
            : "NavButtonStyle");
        TodayNavButton.Style = (Style)FindResource(activeTab == AppNavTab.Today
            ? "ActiveNavButtonStyle"
            : "NavButtonStyle");
        SettingsNavButton.Style = (Style)FindResource(activeTab == AppNavTab.Settings
            ? "ActiveNavButtonStyle"
            : "NavButtonStyle");
        AnimateNavTabIndicator(GetActiveNavButton(activeTab));
    }

    private void SidebarRoot_Loaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(() => PositionNavTabIndicator(GetActiveNavButton(_activeNavTab)), DispatcherPriority.ContextIdle);
    }

    private void SidebarNavButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button && button.IsEnabled && button.Visibility == Visibility.Visible)
        {
            AnimateNavTabIndicator(button, TimeSpan.FromMilliseconds(150));
        }
    }

    private Button GetActiveNavButton(AppNavTab activeTab)
    {
        return activeTab switch
        {
            AppNavTab.Today => TodayNavButton,
            AppNavTab.Settings => SettingsNavButton,
            _ => BibleStudyNavButton
        };
    }

    private void PositionNavTabIndicator(Button targetButton)
    {
        if (NavTabIndicator is null || NavTabIndicatorLayer is null || targetButton is null || !targetButton.IsVisible)
        {
            return;
        }

        var targetPoint = targetButton.TranslatePoint(new Point(0, 0), NavTabIndicatorLayer);
        Canvas.SetLeft(NavTabIndicator, targetPoint.X);
        Canvas.SetTop(NavTabIndicator, targetPoint.Y);
        NavTabIndicator.Width = targetButton.ActualWidth;
        NavTabIndicator.Height = targetButton.ActualHeight;
    }

    private void AnimateNavTabIndicator(Button targetButton)
    {
        AnimateNavTabIndicator(targetButton, TimeSpan.FromMilliseconds(220));
    }

    private void AnimateNavTabIndicator(Button targetButton, TimeSpan duration)
    {
        if (NavTabIndicator is null || NavTabIndicatorLayer is null || targetButton is null || !targetButton.IsVisible)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var targetPoint = targetButton.TranslatePoint(new Point(0, 0), NavTabIndicatorLayer);
                var currentTop = Canvas.GetTop(NavTabIndicator);
                var targetTop = targetPoint.Y;
                var targetLeft = targetPoint.X;
                var targetWidth = targetButton.ActualWidth;
                var targetHeight = targetButton.ActualHeight;
                if (double.IsNaN(currentTop))
                {
                    PositionNavTabIndicator(targetButton);
                    return;
                }

                var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
                NavTabIndicator.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(targetLeft, duration)
                {
                    EasingFunction = ease
                });
                NavTabIndicator.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(targetTop, duration)
                {
                    EasingFunction = ease
                });
                NavTabIndicator.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(targetWidth, duration)
                {
                    EasingFunction = ease
                });
                NavTabIndicator.BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation(targetHeight, TimeSpan.FromMilliseconds(Math.Min(180, duration.TotalMilliseconds)))
                {
                    EasingFunction = ease
                });
            }
            catch
            {
            }
        }, DispatcherPriority.Loaded);
    }

    private void TodayStudy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: WorkspaceItem study })
        {
            return;
        }

        RunPageTransition(sender, () => OpenTodayStudy(study));
    }

    private void OpenTodayStudy(WorkspaceItem study)
    {
        var root = GetRootWorkspace(study);
        _selectedBook = BibleBooks.FirstOrDefault(book => book.Name == root.Name);
        _currentContainer = study.Parent ?? root;
        OpenStudy(study);
        RenderBreadcrumbs();
    }

    private void RenderTodayDashboard()
    {
        MainSubheading.Text = DateTime.Now.ToString("dddd, MMMM d, yyyy");
        _todayEditedStudies.Clear();

        var today = DateTime.Today;
        var dayState = GetTodayState();
        foreach (var study in GetAllStudies()
                     .Where(study => study.LastEditedAt?.Date == today)
                     .Where(study => !dayState.ExcludedStudyContextKeys.Contains(CreateStudyContextKey(study), StringComparer.OrdinalIgnoreCase))
                     .OrderByDescending(study => study.LastEditedAt))
        {
            _todayEditedStudies.Add(CreateDailyStudyEntry(study));
        }

        TodayEditedCountText.Text = _todayEditedStudies.Count == 1
            ? "1 study edited"
            : $"{_todayEditedStudies.Count} studies edited";
        EmptyTodayText.Visibility = _todayEditedStudies.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        _isLoadingDailyDetails = true;
        DailyDetailsTextBox.Text = dayState.Details;
        _isLoadingDailyDetails = false;
        UpdateDailyDetailsPlaceholder();
        RenderScheduledNotifications();
    }

    private void RemoveTodayEditedStudyContext_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not Button { Tag: string contextKey } || string.IsNullOrWhiteSpace(contextKey))
        {
            return;
        }

        var dayState = GetTodayState();
        if (!dayState.ExcludedStudyContextKeys.Contains(contextKey, StringComparer.OrdinalIgnoreCase))
        {
            dayState.ExcludedStudyContextKeys.Add(contextKey);
        }

        SaveWorkspaceState();
        RenderTodayDashboard();
        ShowToast("Deleted from today's AI context");
    }

    private void RenderScheduledNotifications()
    {
        _scheduledNotificationDisplays.Clear();

        foreach (var notification in _scheduledNotifications
                     .OrderByDescending(notification => notification.CreatedAt)
                     .Take(12))
        {
            _scheduledNotificationDisplays.Add(new ScheduledNotificationDisplay(notification));
        }

        var activeCount = _scheduledNotifications.Count(notification => !notification.Deleted);
        var deletedCount = _scheduledNotifications.Count(notification => notification.Deleted);

        ScheduledNotificationSummaryText.Text = activeCount == 0
            ? "No active scheduled messages."
            : activeCount == 1
                ? "1 active scheduled message can be canceled."
                : $"{activeCount} active scheduled messages can be canceled.";
        if (deletedCount > 0)
        {
            ScheduledNotificationSummaryText.Text += $" {deletedCount} deleted.";
        }

        EmptyScheduledNotificationText.Visibility = _scheduledNotificationDisplays.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateCancelScheduledNotificationsButton();
    }

    private void RemoveDeletedScheduledNotification_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string sequenceId })
        {
            return;
        }

        var removedCount = _scheduledNotifications.RemoveAll(notification =>
            notification.Deleted
            && string.Equals(notification.SequenceId, sequenceId, StringComparison.OrdinalIgnoreCase));
        if (removedCount == 0)
        {
            return;
        }

        SaveWorkspaceState();
        RenderScheduledNotifications();
        ShowToast("Deleted message removed");
    }

    private DailyStudyEntry CreateDailyStudyEntry(WorkspaceItem study)
    {
        var root = GetRootWorkspace(study);
        var lineage = GetContainerLineage(study);
        var folders = lineage
            .Skip(1)
            .Take(Math.Max(0, lineage.Count - 2))
            .Select(item => item.Name);
        var folderPath = string.Join(" / ", folders);
        var location = string.IsNullOrWhiteSpace(folderPath)
            ? root.Name
            : $"{root.Name} / {folderPath}";

        return new DailyStudyEntry(
            study,
            study.Name,
            location,
            study.PassageLabel,
            study.LastEditedAt?.ToString("h:mm tt") ?? string.Empty,
            CreateStudyContextKey(study));
    }

    private string CreateStudyContextKey(WorkspaceItem study)
    {
        return string.Join("/", GetContainerLineage(study).Select(item => item.Name));
    }

    private static string TrimForReminder(string text)
    {
        var compact = string.Join(" ", text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= 120
            ? compact
            : $"{compact[..117]}...";
    }

    private static string FillReminderPrompt(string prompt, string studyName, string passage, string details)
    {
        var detailText = string.IsNullOrWhiteSpace(details)
            ? string.Empty
            : TrimForReminder(details);

        var template = string.IsNullOrWhiteSpace(prompt)
            ? "Return to {passage}."
            : prompt;

        return template
            .Replace("{study}", studyName, StringComparison.OrdinalIgnoreCase)
            .Replace("{passage}", passage, StringComparison.OrdinalIgnoreCase)
            .Replace("{details}", detailText, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string NormalizeReminderInstruction(string prompt)
    {
        return string.IsNullOrWhiteSpace(prompt)
            ? "Write a short reminder that brings me back to this study."
            : prompt.Trim();
    }

    private static string NormalizeOverviewInstruction(string prompt)
    {
        return string.IsNullOrWhiteSpace(prompt)
            ? DefaultOverviewPrompt
            : prompt.Trim();
    }

    private void ReminderSettings_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _isLoadingReminderSettings)
        {
            return;
        }

        QueueWorkspaceSave();
        if (sender == OverviewNotificationEnabledCheckBox)
        {
            ShowToast(OverviewNotificationEnabledCheckBox.IsChecked == true
                ? "Overview enabled"
                : "Overview disabled");
            return;
        }

        ShowToast(RemindersEnabledCheckBox.IsChecked == true
            ? "Follow-ups enabled"
            : "Follow-ups disabled");
    }

    private void ReminderSettingsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing || _isLoadingReminderSettings)
        {
            return;
        }

        QueueWorkspaceSave();
    }

    private void AddReminder_Click(object sender, RoutedEventArgs e)
    {
        AddReminderScheduleItem(new ReminderScheduleItem(
            1,
            "hours",
            "Write a short reminder that brings me back to this study."),
            insertFirst: true);
        QueueWorkspaceSave();
        ShowToast("Reminder added");
    }

    private void RemoveReminder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ReminderScheduleItem item })
        {
            return;
        }

        item.PropertyChanged -= ReminderScheduleItem_PropertyChanged;
        _reminderScheduleItems.Remove(item);
        UpdateReminderCount();
        QueueWorkspaceSave();
        ShowToast("Reminder removed");
    }

    private void ReminderPresetSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingReminderSettings || ReminderPresetSelect.SelectedItem is not ReminderPreset preset)
        {
            return;
        }

        NewReminderPresetNameTextBox.Text = preset.Name;
        ApplyReminderPreset(preset);
        QueueWorkspaceSave();
        ShowToast("Preset activated");
    }

    private void ApplyReminderPreset_Click(object sender, RoutedEventArgs e)
    {
        if (ReminderPresetSelect.SelectedItem is not ReminderPreset preset)
        {
            return;
        }

        ApplyReminderPreset(preset);
        QueueWorkspaceSave();
        ShowToast("Preset activated");
    }

    private void ApplyReminderPreset(ReminderPreset preset)
    {
        foreach (var item in _reminderScheduleItems)
        {
            item.PropertyChanged -= ReminderScheduleItem_PropertyChanged;
        }

        _reminderScheduleItems.Clear();
        foreach (var item in preset.Reminders)
        {
            AddReminderScheduleItem(item.Clone());
        }

        UpdateReminderCount();
    }

    private void UpdateReminderCount()
    {
        if (ReminderCountText is null)
        {
            return;
        }

        ReminderCountText.Text = _reminderScheduleItems.Count == 1
            ? "1 follow-up reminder"
            : $"{_reminderScheduleItems.Count} follow-up reminders";
    }

    private void SaveReminderPreset_Click(object sender, RoutedEventArgs e)
    {
        var presetName = string.IsNullOrWhiteSpace(NewReminderPresetNameTextBox.Text)
            ? $"Default {_reminderPresets.Count + 1}"
            : NewReminderPresetNameTextBox.Text.Trim();
        var reminders = _reminderScheduleItems
            .Select(item => item.Clone())
            .ToList();
        var existing = _reminderPresets.FirstOrDefault(preset => preset.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            var index = _reminderPresets.IndexOf(existing);
            _reminderPresets[index] = new ReminderPreset(presetName, reminders);
            _isLoadingReminderSettings = true;
            ReminderPresetSelect.SelectedIndex = index;
            _isLoadingReminderSettings = false;
        }
        else
        {
            var preset = new ReminderPreset(presetName, reminders);
            _reminderPresets.Add(preset);
            _isLoadingReminderSettings = true;
            ReminderPresetSelect.SelectedItem = preset;
            _isLoadingReminderSettings = false;
        }

        QueueWorkspaceSave();
        ShowToast("Default saved");
    }

    private void DailyDetails_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateDailyDetailsPlaceholder();
        if (_isLoadingDailyDetails)
        {
            return;
        }

        var todayState = GetTodayState();
        todayState.Details = DailyDetailsTextBox.Text;
        QueueWorkspaceSave();
    }

    private void UpdateDailyDetailsPlaceholder()
    {
        if (DailyDetailsPlaceholderText is null || DailyDetailsTextBox is null)
        {
            return;
        }

        DailyDetailsPlaceholderText.Visibility = string.IsNullOrWhiteSpace(DailyDetailsTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyColorScheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ColorSchemePreset preset })
        {
            return;
        }

        ApplyColorScheme(preset);
        SaveWorkspaceState();
        ShowToast("Color scheme applied");
    }

    private void PickColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TextBox textBox })
        {
            return;
        }

        var currentColor = TryParseColor(textBox.Text, out var parsedColor)
            ? parsedColor
            : Colors.White;

        if (!ShowColorPickerDialog(currentColor, out var selectedColor))
        {
            return;
        }

        textBox.Text = ToHex(selectedColor);
    }

    private bool ShowColorPickerDialog(Color initialColor, out Color selectedColor)
    {
        selectedColor = initialColor;

        var preview = new Border
        {
            Height = 48,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(initialColor),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var redSlider = CreateColorSlider(initialColor.R);
        var greenSlider = CreateColorSlider(initialColor.G);
        var blueSlider = CreateColorSlider(initialColor.B);
        var valueText = new TextBlock
        {
            Text = ToHex(initialColor),
            Foreground = GetResourceBrush("TextSecondary"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };

        void UpdatePreview(object? _, RoutedPropertyChangedEventArgs<double> __)
        {
            var color = Color.FromRgb((byte)redSlider.Value, (byte)greenSlider.Value, (byte)blueSlider.Value);
            preview.Background = new SolidColorBrush(color);
            valueText.Text = ToHex(color);
        }

        redSlider.ValueChanged += UpdatePreview;
        greenSlider.ValueChanged += UpdatePreview;
        blueSlider.ValueChanged += UpdatePreview;

        var okButton = new Button
        {
            Content = "Use color",
            Padding = new Thickness(14, 8, 14, 8),
            MinWidth = 96,
            Margin = new Thickness(0, 0, 8, 0),
            Style = TryFindResource("AccentButtonStyle") as Style
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(14, 8, 14, 8),
            MinWidth = 82,
            Style = TryFindResource("ChromeButtonStyle") as Style
        };

        var dialog = new Window
        {
            Owner = this,
            Title = "Pick color",
            Width = 360,
            Height = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = GetResourceBrush("AppBackground"),
            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Children =
                {
                    preview,
                    valueText,
                    CreateColorSliderRow("Red", redSlider),
                    CreateColorSliderRow("Green", greenSlider),
                    CreateColorSliderRow("Blue", blueSlider),
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 18, 0, 0),
                        Children = { okButton, cancelButton }
                    }
                }
            }
        };

        okButton.Click += (_, _) => dialog.DialogResult = true;
        cancelButton.Click += (_, _) => dialog.DialogResult = false;

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        selectedColor = Color.FromRgb((byte)redSlider.Value, (byte)greenSlider.Value, (byte)blueSlider.Value);
        return true;
    }

    private static Slider CreateColorSlider(byte value)
    {
        return new Slider
        {
            Minimum = 0,
            Maximum = 255,
            Value = value,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Width = 220
        };
    }

    private StackPanel CreateColorSliderRow(string label, Slider slider)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8),
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Width = 58,
                    Foreground = GetResourceBrush("TextPrimary"),
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                },
                slider
            }
        };
    }

    private static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private void ColorHex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || !TryParseColor(textBox.Text, out var color))
        {
            return;
        }

        var brush = new SolidColorBrush(color);
        if (ReferenceEquals(textBox, NewAppBackgroundTextBox) && NewAppBackgroundSwatch is not null)
        {
            NewAppBackgroundSwatch.Background = brush;
        }
        else if (ReferenceEquals(textBox, NewPanelBackgroundTextBox) && NewPanelBackgroundSwatch is not null)
        {
            NewPanelBackgroundSwatch.Background = brush;
        }
        else if (ReferenceEquals(textBox, NewAccentTextBox) && NewAccentSwatch is not null)
        {
            NewAccentSwatch.Background = brush;
        }
        else if (ReferenceEquals(textBox, NewTextPrimaryTextBox) && NewTextPrimarySwatch is not null)
        {
            NewTextPrimarySwatch.Background = brush;
        }
    }

    private void AddColorScheme_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(NewColorSchemeNameTextBox.Text)
            ? $"Custom {_colorSchemePresets.Count + 1}"
            : NewColorSchemeNameTextBox.Text.Trim();

        var preset = new ColorSchemePreset(
            name,
            NewAppBackgroundTextBox.Text.Trim(),
            NewAppBackgroundTextBox.Text.Trim(),
            NewPanelBackgroundTextBox.Text.Trim(),
            NewAccentTextBox.Text.Trim(),
            NewTextPrimaryTextBox.Text.Trim(),
            NewTextPrimaryTextBox.Text.Trim(),
            false);

        if (!ValidateColorScheme(preset))
        {
            ShowToast("Use valid hex colors");
            return;
        }

        var existing = _colorSchemePresets.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null && !existing.BuiltIn)
        {
            _colorSchemePresets.Remove(existing);
        }

        _colorSchemePresets.Add(preset);
        ApplyColorScheme(preset);
        SaveWorkspaceState();
        ShowToast("Color scheme added");
    }

    private void DeleteColorScheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ColorSchemePreset preset })
        {
            return;
        }

        if (preset.BuiltIn)
        {
            ShowToast("Built-in schemes stay available");
            return;
        }

        _colorSchemePresets.Remove(preset);
        if (string.Equals(_selectedColorSchemeName, preset.Name, StringComparison.OrdinalIgnoreCase)
            && _colorSchemePresets.FirstOrDefault() is ColorSchemePreset fallback)
        {
            ApplyColorScheme(fallback);
        }

        SaveWorkspaceState();
        ShowToast("Color scheme deleted");
    }

    private static bool ValidateColorScheme(ColorSchemePreset preset)
    {
        return TryParseColor(preset.AppBackground, out _)
               && TryParseColor(preset.PanelBackground, out _)
               && TryParseColor(preset.Accent, out _)
               && TryParseColor(preset.TextPrimary, out _);
    }

    private DailyNoteState GetTodayState()
    {
        var dateKey = DateTime.Today.ToString("yyyy-MM-dd");
        if (_dailyNotesByDate.TryGetValue(dateKey, out var state))
        {
            return state;
        }

        state = new DailyNoteState { DateKey = dateKey };
        _dailyNotesByDate[dateKey] = state;
        return state;
    }

    private IEnumerable<WorkspaceItem> GetAllStudies()
    {
        foreach (var root in _workspaceByBook.Values)
        {
            foreach (var study in GetStudyDescendants(root))
            {
                yield return study;
            }
        }
    }

    private static IEnumerable<WorkspaceItem> GetStudyDescendants(WorkspaceItem item)
    {
        if (item.Kind == WorkspaceItemKind.Study)
        {
            yield return item;
        }

        foreach (var child in item.Children)
        {
            foreach (var descendant in GetStudyDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static WorkspaceItem GetRootWorkspace(WorkspaceItem item)
    {
        var cursor = item;
        while (cursor.Parent is not null)
        {
            cursor = cursor.Parent;
        }

        return cursor;
    }

    private void ConfigurePassageSelectors(WorkspaceItem study)
    {
        var root = GetRootWorkspace(study);
        var book = BibleBooks.FirstOrDefault(candidate => candidate.Name == root.Name);
        var chapters = Enumerable.Range(1, Math.Max(1, book?.Chapters ?? 1)).ToList();

        _isLoadingPassageSelection = true;
        StartChapterSelect.ItemsSource = chapters;
        EndChapterSelect.ItemsSource = chapters;
        StartChapterSelect.SelectedItem = study.PassageStartChapter;
        EndChapterSelect.SelectedItem = study.PassageEndChapter;
        RefreshVerseSelectors(book, study.PassageStartChapter, study.PassageEndChapter);
        StartVerseSelect.SelectedItem = ClampVerseSelection(book, study.PassageStartChapter, study.PassageStartVerse);
        EndVerseSelect.SelectedItem = ClampVerseSelection(book, study.PassageEndChapter, study.PassageEndVerse);
        PassagePreviewText.Text = study.PassageLabel;
        _isLoadingPassageSelection = false;
        RenderScripturePanel(study);
    }

    private void PassageSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingPassageSelection || _currentStudy is null)
        {
            return;
        }

        var root = GetRootWorkspace(_currentStudy);
        var book = BibleBooks.FirstOrDefault(candidate => candidate.Name == root.Name);
        var startChapter = GetSelectedInt(StartChapterSelect);
        var endChapter = GetSelectedInt(EndChapterSelect);
        var startVerse = GetSelectedInt(StartVerseSelect);
        var endVerse = GetSelectedInt(EndVerseSelect);

        _isLoadingPassageSelection = true;
        RefreshVerseSelectors(book, startChapter, endChapter);
        StartVerseSelect.SelectedItem = ClampVerseSelection(book, startChapter, startVerse);
        EndVerseSelect.SelectedItem = ClampVerseSelection(book, endChapter, endVerse);
        _isLoadingPassageSelection = false;

        _currentStudy.PassageStartChapter = startChapter;
        _currentStudy.PassageStartVerse = GetSelectedInt(StartVerseSelect);
        _currentStudy.PassageEndChapter = endChapter;
        _currentStudy.PassageEndVerse = GetSelectedInt(EndVerseSelect);
        PassagePreviewText.Text = _currentStudy.PassageLabel;
        RenderScripturePanel(_currentStudy);
        MarkCurrentStudyEdited();
    }

    private void LoadScriptureText()
    {
        foreach (var dataSource in GetScriptureDataSources())
        {
            if (!File.Exists(dataSource.Path))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(dataSource.Path);
                var data = JsonSerializer.Deserialize<BibleTranslationFile>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (data?.Verses is null)
                {
                    continue;
                }

                _scriptureTextByBookChapter.Clear();
                foreach (var verse in data.Verses)
                {
                    if (string.IsNullOrWhiteSpace(verse.BookName) || string.IsNullOrWhiteSpace(verse.Text))
                    {
                        continue;
                    }

                    if (!_scriptureTextByBookChapter.TryGetValue(verse.BookName, out var chapters))
                    {
                        chapters = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                        _scriptureTextByBookChapter[verse.BookName] = chapters;
                    }

                    var chapterKey = verse.Chapter.ToString();
                    if (!chapters.TryGetValue(chapterKey, out var verses))
                    {
                        verses = new List<string>();
                        chapters[chapterKey] = verses;
                    }

                    while (verses.Count < verse.Verse)
                    {
                        verses.Add(string.Empty);
                    }

                    verses[verse.Verse - 1] = verse.Text;
                }

                _scriptureTranslationLabel = dataSource.Label;
                return;
            }
            catch
            {
                _scriptureTextByBookChapter.Clear();
            }
        }
    }

    private static IEnumerable<ScriptureDataSource> GetScriptureDataSources()
    {
        foreach (var fileName in new[] { "nasb1995.json", "asv.json" })
        {
            var label = fileName.Equals("nasb1995.json", StringComparison.OrdinalIgnoreCase)
                ? "NASB1995"
                : "ASV";

            yield return new ScriptureDataSource(
                label,
                Path.Combine(@"C:\Users\jake1\OneDrive\Desktop\Bible Study App", fileName));
            yield return new ScriptureDataSource(label, Path.Combine(AppContext.BaseDirectory, "Data", fileName));
            yield return new ScriptureDataSource(label, Path.Combine(AppContext.BaseDirectory, fileName));

            var cursor = new DirectoryInfo(AppContext.BaseDirectory);
            while (cursor is not null)
            {
                yield return new ScriptureDataSource(label, Path.Combine(cursor.FullName, fileName));
                cursor = cursor.Parent;
            }

            yield return new ScriptureDataSource(
                label,
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Bible Study Studio",
                    fileName));
        }
    }

    private void LoadTagntData()
    {
        _tagntEntriesByReference.Clear();

        var loaded = false;
        foreach (var path in GetTagntDataPaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                ParseTagntRows(File.ReadAllText(path));
                loaded = true;
            }
            catch
            {
                // Keep the scripture panel usable even if a downloaded TAGNT file has an unexpected row.
            }
        }

        if (!loaded)
        {
            ParseTagntRows(GetBuiltInTagntSeed());
        }
    }

    private static IEnumerable<string> GetTagntDataPaths()
    {
        foreach (var fileName in new[] { "tagnt-mat-jhn.txt", "tagnt-act-rev.txt", "TAGNT Mat-Jhn.txt", "TAGNT Act-Rev.txt" })
        {
            yield return Path.Combine(@"C:\Users\jake1\OneDrive\Desktop\Bible Study App", fileName);
            yield return Path.Combine(@"C:\Users\jake1\OneDrive\Desktop\Bible Study App", "Bible Study App", "Data", fileName);
            yield return Path.Combine(AppContext.BaseDirectory, "Data", fileName);
            yield return Path.Combine(AppContext.BaseDirectory, fileName);
        }
    }

    private void LoadGreekLexiconData()
    {
        _greekLexiconByStrong.Clear();
        _greekLexiconLoaded = true;

        foreach (var path in GetGreekLexiconDataPaths().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                ParseGreekLexiconRows(File.ReadLines(path));
            }
            catch
            {
                // Lexicon data is additive; a bad file should not break the scripture reader.
            }
        }
    }

    private static IEnumerable<string> GetGreekLexiconDataPaths()
    {
        foreach (var fileName in new[]
        {
            "tbesg.txt",
            "TBESG.txt",
            "TBESG - Translators Brief lexicon of Extended Strongs for Greek.txt"
        })
        {
            yield return Path.Combine(@"C:\Users\jake1\OneDrive\Desktop\Bible Study App", fileName);
            yield return Path.Combine(@"C:\Users\jake1\OneDrive\Desktop\Bible Study App", "Bible Study App", "Data", fileName);
            yield return Path.Combine(AppContext.BaseDirectory, "Data", fileName);
            yield return Path.Combine(AppContext.BaseDirectory, fileName);
        }

        foreach (var directory in GetDataDirectories())
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "*TBESG*.txt"))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> GetDataDirectories()
    {
        yield return @"C:\Users\jake1\OneDrive\Desktop\Bible Study App";
        yield return Path.Combine(@"C:\Users\jake1\OneDrive\Desktop\Bible Study App", "Bible Study App", "Data");
        yield return Path.Combine(AppContext.BaseDirectory, "Data");
        yield return AppContext.BaseDirectory;
    }

    private void ParseGreekLexiconRows(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (!TryParseGreekLexiconLine(line, out var entry))
            {
                continue;
            }

            CacheGreekLexiconEntry(entry);
        }
    }

    private static bool TryParseGreekLexiconLine(string line, out GreekLexiconEntry entry)
    {
        entry = EmptyGreekLexiconEntry;
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("G", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var cells = line.Split('\t');
        if (cells.Length < 8 || !Regex.IsMatch(cells[0].Trim(), @"^G\d{4,5}[A-Z]?$", RegexOptions.CultureInvariant))
        {
            return false;
        }

        entry = new GreekLexiconEntry(
            cells[0].Trim(),
            cells[1].Trim(),
            cells[2].Trim(),
            cells[3].Trim(),
            cells[4].Trim(),
            cells[5].Trim(),
            cells[6].Trim(),
            CleanLexiconMarkup(cells[7].Trim()));
        return true;
    }

    private void CacheGreekLexiconEntry(GreekLexiconEntry entry)
    {
        foreach (var strong in GetLexiconKeys(entry))
        {
            _greekLexiconByStrong.TryAdd(strong, entry);
        }
    }

    private static IEnumerable<string> GetLexiconKeys(GreekLexiconEntry entry)
    {
        foreach (var value in new[] { entry.EStrong, entry.DStrong, entry.UStrong })
        {
            foreach (var strong in ExtractStrongCandidates(value))
            {
                yield return strong;
            }
        }
    }

    private static IEnumerable<string> ExtractStrongCandidates(params string[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach (Match match in Regex.Matches(value, @"G\d{4,5}[A-Z]?", RegexOptions.CultureInvariant))
            {
                yield return match.Value;
            }
        }
    }

    private GreekLexiconEntry? FindGreekLexiconEntry(TagntWordEntry entry)
    {
        var candidates = ExtractStrongCandidates(entry.DStrong, entry.SimpleStrong, entry.AltStrongs)
            .SelectMany(GetStrongLookupCandidates)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var candidate in candidates)
        {
            if (_greekLexiconByStrong.TryGetValue(candidate, out var exactMatch))
            {
                return exactMatch;
            }
        }

        var searchedEntry = FindGreekLexiconEntryInFiles(candidates);
        if (searchedEntry is not null)
        {
            CacheGreekLexiconEntry(searchedEntry);
        }

        return searchedEntry;
    }

    private GreekLexiconEntry? FindGreekLexiconEntryInFiles(IReadOnlyCollection<string> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        foreach (var path in GetGreekLexiconDataPaths().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    if (!TryParseGreekLexiconLine(line, out var entry))
                    {
                        continue;
                    }

                    if (GetLexiconKeys(entry).Any(key => candidates.Contains(key, StringComparer.OrdinalIgnoreCase)))
                    {
                        return entry;
                    }
                }
            }
            catch
            {
                // Lexicon lookup is best-effort; a bad file should not break Strong's tagging.
            }
        }

        return null;
    }

    private static IEnumerable<string> GetStrongLookupCandidates(string strong)
    {
        if (string.IsNullOrWhiteSpace(strong))
        {
            yield break;
        }

        yield return strong;

        var baseStrong = Regex.Match(strong, @"^G\d{4,5}", RegexOptions.CultureInvariant).Value;
        if (!string.IsNullOrWhiteSpace(baseStrong) && !baseStrong.Equals(strong, StringComparison.OrdinalIgnoreCase))
        {
            yield return baseStrong;
        }
    }

    private void EnsureGreekLexiconDataLoaded()
    {
        if (_greekLexiconLoaded)
        {
            return;
        }

        LoadGreekLexiconData();
    }

    private static string CleanLexiconMarkup(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = value
            .Replace("<BR />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<BR/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<BR>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<ref='([^']+)'>(.*?)</ref>", "$2", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, "<.*?>", string.Empty, RegexOptions.CultureInvariant);
        cleaned = WebUtility.HtmlDecode(cleaned);
        cleaned = Regex.Replace(cleaned, @"[ \t]+", " ", RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n", RegexOptions.CultureInvariant);
        return cleaned.Trim();
    }

    private void ParseTagntRows(string rawText)
    {
        List<TagntWordEntry>? currentEntries = null;

        foreach (var rawLine in rawText.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = line.Split('\t');
            if (cells.Length == 0)
            {
                continue;
            }

            var label = cells[0].Trim();
            if (TryApplyTagntWordDetailRow(cells))
            {
                continue;
            }

            if (label.StartsWith("# ", StringComparison.Ordinal))
            {
                currentEntries = CreateTagntEntries(label[2..].Trim(), cells.Skip(1));
                continue;
            }

            if (currentEntries is null || !label.StartsWith("#_", StringComparison.Ordinal))
            {
                continue;
            }

            ApplyTagntRow(currentEntries, label[2..].Trim(), cells.Skip(1).ToArray());
        }
    }

    private List<TagntWordEntry>? CreateTagntEntries(string referenceText, IEnumerable<string> greekCells)
    {
        if (!TryParseTagntReference(referenceText, out var key))
        {
            return null;
        }

        var entries = greekCells
            .Select((cell, index) => new TagntWordEntry(index + 1, cell.Trim()))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Greek))
            .ToList();
        if (entries.Count == 0)
        {
            return null;
        }

        _tagntEntriesByReference[key] = entries;
        return entries;
    }

    private static void ApplyTagntRow(List<TagntWordEntry> entries, string label, IReadOnlyList<string> values)
    {
        var normalizedLabel = label.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        for (var index = 0; index < entries.Count && index < values.Count; index++)
        {
            var value = values[index].Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (normalizedLabel)
            {
                case "translation":
                case "english":
                    entries[index].English = value;
                    break;
                case "word=grammar":
                    ApplyTagntStrongGrammar(entries[index], value);
                    break;
                case "dstrong":
                case "strong":
                case "strongs":
                    entries[index].DStrong = value;
                    break;
                case "grammar":
                case "morphology":
                    entries[index].Grammar = value;
                    break;
                case "dictionaryform&gloss":
                case "dictionaryform":
                    entries[index].DictionaryForm = value;
                    break;
                case "gloss":
                case "sub-meanings":
                case "submeanings":
                    entries[index].Gloss = value;
                    break;
                case "wordtype":
                    entries[index].WordType = value;
                    break;
                case "editions":
                    entries[index].Editions = value;
                    break;
                case "variants":
                case "variantnotes":
                    entries[index].VariantNotes = value;
                    break;
            }
        }
    }

    private bool TryApplyTagntWordDetailRow(IReadOnlyList<string> cells)
    {
        if (cells.Count < 4)
        {
            return false;
        }

        var match = Regex.Match(
            cells[0].Trim(),
            @"^(?<book>[1-3]?[A-Za-z]{2,3})\.(?<chapter>\d+)\.(?<verse>\d+)#(?<word>\d+)=(?<type>[^\t]+)$",
            RegexOptions.CultureInvariant);
        if (!match.Success
            || !TryMapTagntBookCode(match.Groups["book"].Value, out var bookName)
            || !int.TryParse(match.Groups["chapter"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chapter)
            || !int.TryParse(match.Groups["verse"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var verse)
            || !int.TryParse(match.Groups["word"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wordIndex))
        {
            return false;
        }

        var key = new ScriptureReferenceKey(bookName, chapter, verse);
        if (!_tagntEntriesByReference.TryGetValue(key, out var entries))
        {
            entries = new List<TagntWordEntry>();
            _tagntEntriesByReference[key] = entries;
        }

        var entry = entries.FirstOrDefault(candidate => candidate.WordIndex == wordIndex);
        if (entry is null)
        {
            entry = new TagntWordEntry(wordIndex, cells.Count > 1 ? cells[1].Trim() : string.Empty);
            entries.Add(entry);
            entries.Sort((left, right) => left.WordIndex.CompareTo(right.WordIndex));
        }
        else if (string.IsNullOrWhiteSpace(entry.Greek) && cells.Count > 1)
        {
            entry.Greek = cells[1].Trim();
        }

        entry.WordType = match.Groups["type"].Value.Trim();
        if (cells.Count > 2)
        {
            entry.English = cells[2].Trim();
        }

        if (cells.Count > 3)
        {
            ApplyTagntStrongGrammar(entry, cells[3].Trim());
        }

        if (cells.Count > 4)
        {
            ApplyDictionaryGloss(entry, cells[4].Trim());
        }

        if (cells.Count > 5)
        {
            entry.Editions = cells[5].Trim();
        }

        entry.VariantNotes = string.Join(
            "\n",
            new[] { CellAt(cells, 6), CellAt(cells, 7) }.Where(value => !string.IsNullOrWhiteSpace(value)));
        entry.SubMeaning = CellAt(cells, 9);
        entry.ConjoinedWord = CellAt(cells, 10);
        entry.SimpleStrong = CellAt(cells, 11);
        entry.AltStrongs = CellAt(cells, 12);
        return true;
    }

    private static string CellAt(IReadOnlyList<string> cells, int index)
    {
        return index < cells.Count
            ? cells[index].Trim()
            : string.Empty;
    }

    private static void ApplyTagntStrongGrammar(TagntWordEntry entry, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var splitIndex = value.LastIndexOf('=');
        if (splitIndex > 0 && splitIndex < value.Length - 1)
        {
            entry.DStrong = value[..splitIndex].Trim();
            entry.Grammar = value[(splitIndex + 1)..].Trim();
            return;
        }

        entry.DStrong = value.Trim();
    }

    private static void ApplyDictionaryGloss(TagntWordEntry entry, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var splitIndex = value.IndexOf('=');
        if (splitIndex > 0 && splitIndex < value.Length - 1)
        {
            entry.DictionaryForm = value[..splitIndex].Trim();
            entry.Gloss = value[(splitIndex + 1)..].Trim();
            return;
        }

        entry.Gloss = value.Trim();
    }

    private static bool TryParseTagntReference(string referenceText, out ScriptureReferenceKey key)
    {
        key = default;
        var parts = referenceText.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3
            || !TryMapTagntBookCode(parts[0], out var bookName)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var chapter)
            || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var verse))
        {
            return false;
        }

        key = new ScriptureReferenceKey(bookName, chapter, verse);
        return true;
    }

    private static bool TryMapTagntBookCode(string code, out string bookName)
    {
        var normalized = code.Trim();
        var books = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mat"] = "Matthew",
            ["Mrk"] = "Mark",
            ["Mar"] = "Mark",
            ["Luk"] = "Luke",
            ["Jhn"] = "John",
            ["Joh"] = "John",
            ["Act"] = "Acts",
            ["Rom"] = "Romans",
            ["1Co"] = "1 Corinthians",
            ["2Co"] = "2 Corinthians",
            ["Gal"] = "Galatians",
            ["Eph"] = "Ephesians",
            ["Php"] = "Philippians",
            ["Col"] = "Colossians",
            ["1Th"] = "1 Thessalonians",
            ["2Th"] = "2 Thessalonians",
            ["1Ti"] = "1 Timothy",
            ["2Ti"] = "2 Timothy",
            ["Tit"] = "Titus",
            ["Phm"] = "Philemon",
            ["Heb"] = "Hebrews",
            ["Jas"] = "James",
            ["1Pe"] = "1 Peter",
            ["2Pe"] = "2 Peter",
            ["1Jn"] = "1 John",
            ["2Jn"] = "2 John",
            ["3Jn"] = "3 John",
            ["Jud"] = "Jude",
            ["Rev"] = "Revelation"
        };

        return books.TryGetValue(normalized, out bookName!);
    }

    private static string GetBuiltInTagntSeed()
    {
        return "# Mat.1.1\t\u0392\u03af\u03b2\u03bb\u03bf\u03c2\t\u03b3\u03b5\u03bd\u03ad\u03c3\u03b5\u03c9\u03c2\t\u1f38\u03b7\u03c3\u03bf\u1fe6\t\u03a7\u03c1\u03b9\u03c3\u03c4\u03bf\u1fe6\t\u03c5\u1f31\u03bf\u1fe6\t\u0394\u03b1\u03c5\u1f76\u03b4\t\u03c5\u1f31\u03bf\u1fe6\t\u1f08\u03b2\u03c1\u03b1\u1f71\u03bc.\n"
            + "#_Translation\t[The] book\tof [the] genealogy\tof Jesus\tChrist\tson\tof David\tson\tof Abraham.";
    }

    private void RenderScripturePanel(WorkspaceItem study, int? requestedChapter = null, bool scrollToSelection = true)
    {
        var root = GetRootWorkspace(study);
        var book = BibleBooks.FirstOrDefault(candidate => candidate.Name == root.Name);
        var startChapter = study.PassageStartChapter ?? 1;
        var maxChapter = Math.Max(1, book?.Chapters ?? 1);
        var chapter = Math.Clamp(requestedChapter ?? startChapter, 1, maxChapter);
        var startVerse = study.PassageStartVerse;
        var endChapter = study.PassageEndChapter ?? chapter;
        var endVerse = study.PassageEndVerse;
        var verseCount = GetVerseCount(book, chapter);

        _scriptureVisibleBookName = root.Name;
        _scriptureVisibleChapter = chapter;
        ScripturePanelTitle.Text = $"{root.Name} - Chapter {chapter}";
        ScripturePanelSubtitle.Text = $"{_scriptureTranslationLabel} | Selected passage: {study.PassageLabel}";
        ScripturePreviousChapterButton.Visibility = chapter > 1 ? Visibility.Visible : Visibility.Collapsed;
        ScriptureNextChapterButton.Visibility = chapter < maxChapter ? Visibility.Visible : Visibility.Collapsed;
        _scriptureVerses.Clear();
        _scriptureParagraphsByVerse.Clear();
        ScriptureDocument.Blocks.Clear();

        if (!TryGetChapterVerses(root.Name, chapter, out var verses))
        {
            ScriptureScrollViewer.Visibility = Visibility.Collapsed;
            ScriptureSelectionRail.Visibility = Visibility.Collapsed;
            ScriptureSelectionMarker.Visibility = Visibility.Collapsed;
            ScriptureMissingText.Visibility = Visibility.Visible;
            ScriptureMissingText.Text =
                "Scripture text is not configured yet.\n\nPlace nasb1995.json or asv.json at the project root or:\n"
                + string.Join("\n", GetScriptureDataSources().Take(6).Select(dataSource => dataSource.Path));
            return;
        }

        ScriptureMissingText.Visibility = Visibility.Collapsed;
        ScriptureScrollViewer.Visibility = Visibility.Visible;

        for (var index = 0; index < Math.Min(verseCount, verses.Count); index++)
        {
            var verseNumber = index + 1;
            _scriptureVerses.Add(new ScriptureVerseDisplay(
                verseNumber,
                verses[index],
                IsVerseSelected(chapter, verseNumber, startChapter, startVerse, endChapter, endVerse)));
        }

        RenderScriptureDocument();

        Dispatcher.BeginInvoke(() =>
        {
            if (scrollToSelection && _scriptureVerses.Any(verse => verse.IsSelected))
            {
                ScrollScriptureToSelectedVerse();
            }
            else
            {
                ScriptureScrollViewer.ScrollToTop();
            }

            UpdateScriptureSelectionMarker();
        }, DispatcherPriority.Background);
    }

    private bool TryGetChapterVerses(string bookName, int chapter, out List<string> verses)
    {
        verses = new List<string>();
        if (!_scriptureTextByBookChapter.TryGetValue(bookName, out var chapters)
            || !chapters.TryGetValue(chapter.ToString(), out var chapterVerses))
        {
            return false;
        }

        verses = chapterVerses;
        return true;
    }

    private static bool IsVerseSelected(
        int visibleChapter,
        int visibleVerse,
        int startChapter,
        int? startVerse,
        int endChapter,
        int? endVerse)
    {
        if (startVerse is null)
        {
            return false;
        }

        var effectiveEndVerse = endVerse ?? startVerse.Value;
        var afterStart = visibleChapter > startChapter
            || (visibleChapter == startChapter && visibleVerse >= startVerse.Value);
        var beforeEnd = visibleChapter < endChapter
            || (visibleChapter == endChapter && visibleVerse <= effectiveEndVerse);
        return afterStart && beforeEnd;
    }

    private void ScripturePreviousChapter_Click(object sender, RoutedEventArgs e)
    {
        NavigateScriptureChapter(-1);
    }

    private void ScriptureNextChapter_Click(object sender, RoutedEventArgs e)
    {
        NavigateScriptureChapter(1);
    }

    private void NavigateScriptureChapter(int direction)
    {
        if (_currentStudy is null)
        {
            return;
        }

        var root = GetRootWorkspace(_currentStudy);
        var book = BibleBooks.FirstOrDefault(candidate => candidate.Name == root.Name);
        var maxChapter = Math.Max(1, book?.Chapters ?? 1);
        var currentChapter = _scriptureVisibleChapter ?? _currentStudy.PassageStartChapter ?? 1;
        var nextChapter = Math.Clamp(currentChapter + direction, 1, maxChapter);
        if (nextChapter == currentChapter)
        {
            return;
        }

        RenderScripturePanel(_currentStudy, nextChapter, scrollToSelection: false);
    }

    private void ScriptureScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_strongsEnabled)
        {
            return;
        }

        UpdateScriptureSelectionMarker();
    }

    private void ScriptureScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_strongsEnabled)
        {
            return;
        }

        UpdateScriptureSelectionMarker();
    }

    private void ScriptureScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        RevealScrollBarsWhileScrolling(scrollViewer);

        var currentTarget = _scriptureSmoothScrollActive
            ? _scriptureSmoothScrollTargetOffset
            : scrollViewer.VerticalOffset;
        var scrollSpeed = _strongsEnabled ? 1.05 : 0.55;
        _scriptureSmoothScrollTargetOffset = Math.Clamp(
            currentTarget - (e.Delta * scrollSpeed),
            0,
            scrollViewer.ScrollableHeight);
        StartScriptureSmoothScroll();
        e.Handled = true;
    }

    private void StartScriptureSmoothScroll()
    {
        if (_scriptureSmoothScrollActive)
        {
            return;
        }

        _scriptureSmoothScrollActive = true;
        _scriptureSmoothScrollLastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += ScriptureSmoothScroll_Rendering;
    }

    private void StopScriptureSmoothScroll()
    {
        if (!_scriptureSmoothScrollActive)
        {
            return;
        }

        _scriptureSmoothScrollActive = false;
        CompositionTarget.Rendering -= ScriptureSmoothScroll_Rendering;
    }

    private void ScriptureSmoothScroll_Rendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Clamp((now - _scriptureSmoothScrollLastFrame).TotalSeconds, 0.001, 0.05);
        _scriptureSmoothScrollLastFrame = now;

        var targetOffset = Math.Clamp(_scriptureSmoothScrollTargetOffset, 0, ScriptureScrollViewer.ScrollableHeight);
        var distance = targetOffset - ScriptureScrollViewer.VerticalOffset;
        if (Math.Abs(distance) < 0.5)
        {
            ScriptureScrollViewer.ScrollToVerticalOffset(targetOffset);
            StopScriptureSmoothScroll();
            return;
        }

        var progress = 1 - Math.Exp(-18 * elapsedSeconds);
        ScriptureScrollViewer.ScrollToVerticalOffset(ScriptureScrollViewer.VerticalOffset + (distance * progress));
    }

    private void BibleBooksScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        RevealScrollBarsWhileScrolling(scrollViewer);

        var currentTarget = _bibleBooksSmoothScrollActive
            ? _bibleBooksSmoothScrollTargetOffset
            : scrollViewer.VerticalOffset;
        _bibleBooksSmoothScrollTargetOffset = Math.Clamp(
            currentTarget - (e.Delta * 0.55),
            0,
            scrollViewer.ScrollableHeight);
        StartBibleBooksSmoothScroll();
        e.Handled = true;
    }

    private void StartBibleBooksSmoothScroll()
    {
        if (_bibleBooksSmoothScrollActive)
        {
            return;
        }

        _bibleBooksSmoothScrollActive = true;
        _bibleBooksSmoothScrollLastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += BibleBooksSmoothScroll_Rendering;
    }

    private void StopBibleBooksSmoothScroll()
    {
        if (!_bibleBooksSmoothScrollActive)
        {
            return;
        }

        _bibleBooksSmoothScrollActive = false;
        CompositionTarget.Rendering -= BibleBooksSmoothScroll_Rendering;
    }

    private void BibleBooksSmoothScroll_Rendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Clamp((now - _bibleBooksSmoothScrollLastFrame).TotalSeconds, 0.001, 0.05);
        _bibleBooksSmoothScrollLastFrame = now;

        var targetOffset = Math.Clamp(_bibleBooksSmoothScrollTargetOffset, 0, BibleBooksScrollViewer.ScrollableHeight);
        var distance = targetOffset - BibleBooksScrollViewer.VerticalOffset;
        if (Math.Abs(distance) < 0.5)
        {
            BibleBooksScrollViewer.ScrollToVerticalOffset(targetOffset);
            StopBibleBooksSmoothScroll();
            return;
        }

        var progress = 1 - Math.Exp(-18 * elapsedSeconds);
        BibleBooksScrollViewer.ScrollToVerticalOffset(BibleBooksScrollViewer.VerticalOffset + (distance * progress));
    }

    private void StudyEditorScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        RevealScrollBarsWhileScrolling(scrollViewer);

        var currentTarget = _studyEditorSmoothScrollActive
            ? _studyEditorSmoothScrollTargetOffset
            : scrollViewer.VerticalOffset;
        _studyEditorSmoothScrollTargetOffset = Math.Clamp(
            currentTarget - (e.Delta * 0.55),
            0,
            scrollViewer.ScrollableHeight);
        StartStudyEditorSmoothScroll();
        e.Handled = true;
    }

    private void StartStudyEditorSmoothScroll()
    {
        if (_studyEditorSmoothScrollActive)
        {
            return;
        }

        _studyEditorSmoothScrollActive = true;
        _studyEditorSmoothScrollLastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += StudyEditorSmoothScroll_Rendering;
    }

    private void StopStudyEditorSmoothScroll()
    {
        if (!_studyEditorSmoothScrollActive)
        {
            return;
        }

        _studyEditorSmoothScrollActive = false;
        CompositionTarget.Rendering -= StudyEditorSmoothScroll_Rendering;
    }

    private void StudyEditorSmoothScroll_Rendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Clamp((now - _studyEditorSmoothScrollLastFrame).TotalSeconds, 0.001, 0.05);
        _studyEditorSmoothScrollLastFrame = now;

        var targetOffset = Math.Clamp(_studyEditorSmoothScrollTargetOffset, 0, StudyEditorScrollViewer.ScrollableHeight);
        var distance = targetOffset - StudyEditorScrollViewer.VerticalOffset;
        if (Math.Abs(distance) < 0.5)
        {
            StudyEditorScrollViewer.ScrollToVerticalOffset(targetOffset);
            UpdateSmoothEditorCaret(animate: false);
            StopStudyEditorSmoothScroll();
            return;
        }

        var progress = 1 - Math.Exp(-18 * elapsedSeconds);
        StudyEditorScrollViewer.ScrollToVerticalOffset(StudyEditorScrollViewer.VerticalOffset + (distance * progress));
        UpdateSmoothEditorCaret(animate: false);
    }

    private void TodayScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        if (FindAncestor<ScrollViewer>(e.OriginalSource as DependencyObject) is { } nestedScrollViewer
            && !ReferenceEquals(nestedScrollViewer, scrollViewer)
            && nestedScrollViewer.ScrollableHeight > 0)
        {
            return;
        }

        RevealScrollBarsWhileScrolling(scrollViewer);

        var currentTarget = _todaySmoothScrollActive
            ? _todaySmoothScrollTargetOffset
            : scrollViewer.VerticalOffset;
        _todaySmoothScrollTargetOffset = Math.Clamp(
            currentTarget - (e.Delta * 0.55),
            0,
            scrollViewer.ScrollableHeight);
        StartTodaySmoothScroll();
        e.Handled = true;
    }

    private void StartTodaySmoothScroll()
    {
        if (_todaySmoothScrollActive)
        {
            return;
        }

        _todaySmoothScrollActive = true;
        _todaySmoothScrollLastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += TodaySmoothScroll_Rendering;
    }

    private void StopTodaySmoothScroll()
    {
        if (!_todaySmoothScrollActive)
        {
            return;
        }

        _todaySmoothScrollActive = false;
        CompositionTarget.Rendering -= TodaySmoothScroll_Rendering;
    }

    private void TodaySmoothScroll_Rendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Clamp((now - _todaySmoothScrollLastFrame).TotalSeconds, 0.001, 0.05);
        _todaySmoothScrollLastFrame = now;

        var targetOffset = Math.Clamp(_todaySmoothScrollTargetOffset, 0, TodayScrollViewer.ScrollableHeight);
        var distance = targetOffset - TodayScrollViewer.VerticalOffset;
        if (Math.Abs(distance) < 0.5)
        {
            TodayScrollViewer.ScrollToVerticalOffset(targetOffset);
            StopTodaySmoothScroll();
            return;
        }

        var progress = 1 - Math.Exp(-18 * elapsedSeconds);
        TodayScrollViewer.ScrollToVerticalOffset(TodayScrollViewer.VerticalOffset + (distance * progress));
    }

    private void ScheduledMessagesScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        RevealScrollBarsWhileScrolling(scrollViewer);

        var currentTarget = _scheduledMessagesSmoothScrollActive
            ? _scheduledMessagesSmoothScrollTargetOffset
            : scrollViewer.VerticalOffset;
        _scheduledMessagesSmoothScrollTargetOffset = Math.Clamp(
            currentTarget - (e.Delta * 0.55),
            0,
            scrollViewer.ScrollableHeight);
        StartScheduledMessagesSmoothScroll();
        e.Handled = true;
    }

    private void StartScheduledMessagesSmoothScroll()
    {
        if (_scheduledMessagesSmoothScrollActive)
        {
            return;
        }

        _scheduledMessagesSmoothScrollActive = true;
        _scheduledMessagesSmoothScrollLastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += ScheduledMessagesSmoothScroll_Rendering;
    }

    private void StopScheduledMessagesSmoothScroll()
    {
        if (!_scheduledMessagesSmoothScrollActive)
        {
            return;
        }

        _scheduledMessagesSmoothScrollActive = false;
        CompositionTarget.Rendering -= ScheduledMessagesSmoothScroll_Rendering;
    }

    private void ScheduledMessagesSmoothScroll_Rendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Clamp((now - _scheduledMessagesSmoothScrollLastFrame).TotalSeconds, 0.001, 0.05);
        _scheduledMessagesSmoothScrollLastFrame = now;

        var targetOffset = Math.Clamp(_scheduledMessagesSmoothScrollTargetOffset, 0, ScheduledMessagesScrollViewer.ScrollableHeight);
        var distance = targetOffset - ScheduledMessagesScrollViewer.VerticalOffset;
        if (Math.Abs(distance) < 0.5)
        {
            ScheduledMessagesScrollViewer.ScrollToVerticalOffset(targetOffset);
            StopScheduledMessagesSmoothScroll();
            return;
        }

        var progress = 1 - Math.Exp(-18 * elapsedSeconds);
        ScheduledMessagesScrollViewer.ScrollToVerticalOffset(ScheduledMessagesScrollViewer.VerticalOffset + (distance * progress));
    }

    private void StrongsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        RevealScrollBarsWhileScrolling(scrollViewer);

        var currentTarget = _strongsSmoothScrollActive
            ? _strongsSmoothScrollTargetOffset
            : scrollViewer.VerticalOffset;
        _strongsSmoothScrollTargetOffset = Math.Clamp(
            currentTarget - (e.Delta * 0.55),
            0,
            scrollViewer.ScrollableHeight);
        StartStrongsSmoothScroll();
        e.Handled = true;
    }

    private void StartStrongsSmoothScroll()
    {
        if (_strongsSmoothScrollActive)
        {
            return;
        }

        _strongsSmoothScrollActive = true;
        _strongsSmoothScrollLastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += StrongsSmoothScroll_Rendering;
    }

    private void StopStrongsSmoothScroll()
    {
        if (!_strongsSmoothScrollActive)
        {
            return;
        }

        _strongsSmoothScrollActive = false;
        CompositionTarget.Rendering -= StrongsSmoothScroll_Rendering;
    }

    private void StrongsSmoothScroll_Rendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Clamp((now - _strongsSmoothScrollLastFrame).TotalSeconds, 0.001, 0.05);
        _strongsSmoothScrollLastFrame = now;

        var targetOffset = Math.Clamp(_strongsSmoothScrollTargetOffset, 0, StrongsScrollViewer.ScrollableHeight);
        var distance = targetOffset - StrongsScrollViewer.VerticalOffset;
        if (Math.Abs(distance) < 0.5)
        {
            StrongsScrollViewer.ScrollToVerticalOffset(targetOffset);
            StopStrongsSmoothScroll();
            return;
        }

        var progress = 1 - Math.Exp(-18 * elapsedSeconds);
        StrongsScrollViewer.ScrollToVerticalOffset(StrongsScrollViewer.VerticalOffset + (distance * progress));
    }

    private void RevealScrollBarsWhileScrolling(ScrollViewer scrollViewer)
    {
        SetScrollBarScrollingState(scrollViewer, true);

        if (!_scrollBarRevealTimers.TryGetValue(scrollViewer, out var timer))
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1400)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                SetScrollBarScrollingState(scrollViewer, false);
            };
            _scrollBarRevealTimers[scrollViewer] = timer;
        }

        timer.Stop();
        timer.Start();
    }

    private static void SetScrollBarScrollingState(DependencyObject root, bool isScrolling)
    {
        foreach (var scrollBar in FindOwnedScrollBars(root))
        {
            scrollBar.Tag = isScrolling ? "Scrolling" : null;
        }
    }

    private void StopScrollBarRevealTimers()
    {
        foreach (var timer in _scrollBarRevealTimers.Values)
        {
            timer.Stop();
        }

        _scrollBarRevealTimers.Clear();
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<ScrollBar> FindOwnedScrollBars(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is ScrollBar scrollBar)
            {
                yield return scrollBar;
                continue;
            }

            if (child is ScrollViewer && !ReferenceEquals(child, root))
            {
                continue;
            }

            foreach (var descendant in FindOwnedScrollBars(child))
            {
                yield return descendant;
            }
        }
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void ScriptureFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScriptureFontSizeText is null)
        {
            return;
        }

        ScriptureFontSizeText.Text = Math.Round(e.NewValue).ToString(CultureInfo.InvariantCulture);
        if (ScriptureDocument is not null)
        {
            ScriptureDocument.FontSize = e.NewValue;
            foreach (var block in ScriptureDocument.Blocks)
            {
                block.FontSize = e.NewValue;
            }
        }
        if (ScripturePanelTitle is not null)
        {
            ScripturePanelTitle.FontSize = e.NewValue + 4;
        }

        if (!_strongsEnabled)
        {
            Dispatcher.BeginInvoke(() => UpdateScriptureSelectionMarker(), DispatcherPriority.Background);
        }
    }

    private void ScrollScriptureToSelectedVerse()
    {
        var firstSelected = _scriptureVerses.FirstOrDefault(verse => verse.IsSelected)
            ?? _scriptureVerses.FirstOrDefault();
        if (firstSelected is null)
        {
            return;
        }

        if (_scriptureParagraphsByVerse.TryGetValue(firstSelected.VerseNumber, out var paragraph))
        {
            paragraph.BringIntoView();
        }
    }

    private void UpdateScriptureSelectionMarker()
    {
        var selectedVerses = _scriptureVerses.Where(verse => verse.IsSelected).ToList();
        if (selectedVerses.Count == 0 || _scriptureVerses.Count == 0 || ScriptureSelectionRail.ActualHeight <= 0)
        {
            ScriptureSelectionRail.Visibility = Visibility.Collapsed;
            ScriptureSelectionMarker.Visibility = Visibility.Collapsed;
            return;
        }

        var selectedParagraphs = selectedVerses
            .Select(verse => _scriptureParagraphsByVerse.TryGetValue(verse.VerseNumber, out var paragraph) ? paragraph : null)
            .Where(paragraph => paragraph is not null)
            .Cast<Paragraph>()
            .ToList();
        if (selectedParagraphs.Count == 0 || ScriptureTextView.ActualHeight <= 0)
        {
            Dispatcher.BeginInvoke(() => UpdateScriptureSelectionMarker(), DispatcherPriority.Background);
            return;
        }

        var selectedTop = selectedParagraphs.Min(paragraph => paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward).Y);
        var selectedBottom = selectedParagraphs.Max(paragraph => paragraph.ContentEnd.GetCharacterRect(LogicalDirection.Backward).Bottom);
        var contentHeight = Math.Max(1, ScriptureTextView.ExtentHeight > 0 ? ScriptureTextView.ExtentHeight : ScriptureTextView.ActualHeight);
        var railHeight = ScriptureSelectionRail.ActualHeight;
        var markerTopRatio = Math.Clamp(selectedTop / contentHeight, 0, 1);
        var markerHeightRatio = Math.Clamp((selectedBottom - selectedTop) / contentHeight, 0.01, 1);
        var markerHeight = Math.Max(18, railHeight * markerHeightRatio);
        var markerTop = Math.Min(Math.Max(0, railHeight * markerTopRatio), Math.Max(0, railHeight - markerHeight));

        ScriptureSelectionRail.Visibility = Visibility.Visible;
        ScriptureSelectionMarker.Visibility = Visibility.Visible;
        ScriptureSelectionMarker.Height = markerHeight;
        Canvas.SetTop(ScriptureSelectionMarker, markerTop);
    }

    private void RenderScriptureDocument()
    {
        _scriptureParagraphsByVerse.Clear();
        ScriptureDocument.Blocks.Clear();
        ScriptureDocument.Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 17));
        ScriptureDocument.FontSize = ScriptureFontSizeSlider.Value;

        foreach (var verse in _scriptureVerses)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0, 0, 0, 3),
                Padding = new Thickness(6, 4, 6, 4),
                Background = _strongsEnabled
                    ? Brushes.Transparent
                    : CreateAnimatedVerseHighlightBrush(verse.IsSelected)
            };

            paragraph.Inlines.Add(new Run(verse.VerseNumber.ToString(CultureInfo.InvariantCulture))
            {
                FontWeight = FontWeights.Black,
                BaselineAlignment = BaselineAlignment.Superscript,
                FontSize = 11
            });
            paragraph.Inlines.Add(new Run(" "));
            if (_strongsEnabled
                && _scriptureVisibleBookName is not null
                && _scriptureVisibleChapter is { } visibleChapter
                && _tagntEntriesByReference.TryGetValue(
                    new ScriptureReferenceKey(_scriptureVisibleBookName, visibleChapter, verse.VerseNumber),
                    out var tagntEntries))
            {
                AddTaggedScriptureRuns(paragraph, verse, tagntEntries);
            }
            else
            {
                paragraph.Inlines.Add(new Run(verse.Text));
            }

            ScriptureDocument.Blocks.Add(paragraph);
            _scriptureParagraphsByVerse[verse.VerseNumber] = paragraph;
        }
    }

    private void AddTaggedScriptureRuns(Paragraph paragraph, ScriptureVerseDisplay verse, IReadOnlyList<TagntWordEntry> tagntEntries)
    {
        var matches = FindTagntMatches(verse.Text, tagntEntries);
        if (matches.Count == 0)
        {
            paragraph.Inlines.Add(new Run(verse.Text));
            return;
        }

        var cursor = 0;
        var linkIndex = 0;
        foreach (var match in matches.OrderBy(match => match.Start))
        {
            if (match.Start < cursor)
            {
                continue;
            }

            if (match.Start > cursor)
            {
                paragraph.Inlines.Add(new Run(verse.Text[cursor..match.Start]));
            }

            var phrase = verse.Text.Substring(match.Start, match.Length);
            var link = new Hyperlink(new Run(phrase))
            {
                Cursor = Cursors.Hand,
                Foreground = GetStrongsLinkBrush(linkIndex),
                FontWeight = FontWeights.SemiBold,
                TextDecorations = null,
                Tag = new StrongsSelection(_scriptureVisibleBookName ?? string.Empty, _scriptureVisibleChapter ?? 0, verse.VerseNumber, phrase, match.Entry)
            };
            link.Click += StrongsPhrase_Click;
            paragraph.Inlines.Add(link);
            cursor = match.Start + match.Length;
            linkIndex++;
        }

        if (cursor < verse.Text.Length)
        {
            paragraph.Inlines.Add(new Run(verse.Text[cursor..]));
        }
    }

    private Brush GetStrongsLinkBrush(int index)
    {
        return (index % 3) switch
        {
            0 => GetResourceBrush("Coral"),
            1 => GetResourceBrush("PanelBackground"),
            _ => GetResourceBrush("Mint")
        };
    }

    private static List<TagntTextMatch> FindTagntMatches(string verseText, IReadOnlyList<TagntWordEntry> tagntEntries)
    {
        var matches = new List<TagntTextMatch>();
        foreach (var entry in tagntEntries)
        {
            foreach (var phrase in BuildEnglishPhraseCandidates(entry.English))
            {
                var match = Regex.Match(
                    verseText,
                    $@"(?<!\w){Regex.Escape(phrase)}(?!\w)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!match.Success || matches.Any(existing => RangesOverlap(existing.Start, existing.Length, match.Index, match.Length)))
                {
                    continue;
                }

                matches.Add(new TagntTextMatch(match.Index, match.Length, entry));
                break;
            }
        }

        return matches;
    }

    private static IEnumerable<string> BuildEnglishPhraseCandidates(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            yield break;
        }

        var cleaned = phrase.Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal)
            .Trim();
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        cleaned = cleaned.Trim(' ', '.', ',', ';', ':', '!', '?');
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            yield return cleaned;
        }

        var withoutBracketed = Regex.Replace(phrase, @"\[[^\]]+\]", string.Empty);
        withoutBracketed = Regex.Replace(withoutBracketed, @"\s+", " ").Trim(' ', '.', ',', ';', ':', '!', '?');
        if (!string.IsNullOrWhiteSpace(withoutBracketed) && !withoutBracketed.Equals(cleaned, StringComparison.OrdinalIgnoreCase))
        {
            yield return withoutBracketed;
        }

        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            yield return words[^1];
        }
    }

    private static bool RangesOverlap(int firstStart, int firstLength, int secondStart, int secondLength)
    {
        var firstEnd = firstStart + firstLength;
        var secondEnd = secondStart + secondLength;
        return firstStart < secondEnd && secondStart < firstEnd;
    }

    private Brush CreateAnimatedVerseHighlightBrush(bool isSelected)
    {
        if (!isSelected)
        {
            return Brushes.Transparent;
        }

        var targetColor = GetResourceBrush("Mint") is SolidColorBrush mintBrush
            ? mintBrush.Color
            : Color.FromRgb(221, 161, 94);
        var brush = new SolidColorBrush(Color.FromArgb(0, targetColor.R, targetColor.G, targetColor.B));
        brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(
            brush.Color,
            targetColor,
            TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        return brush;
    }

    private async void EndStudyNotification_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStudy is null)
        {
            return;
        }

        SaveWorkspaceState();

        var button = sender as Button;
        if (button is not null)
        {
            button.IsEnabled = false;
            button.Content = "Sending...";
        }

        try
        {
            var apiResponse = await EndStudySessionThroughBackendAsync(_currentStudy);
            var sentCount = apiResponse.Notifications.Count(notification => notification.Sent);
            var failedCount = apiResponse.Notifications.Count - sentCount;
            var failureSummary = failedCount == 0
                ? string.Empty
                : $"\n\nFirst failure: {apiResponse.Notifications.First(notification => !notification.Sent).Error}";
            var overviewSummary = string.IsNullOrWhiteSpace(apiResponse.Overview)
                ? string.Empty
                : $"\n\nOverview: {apiResponse.Overview}";
            var deliverySummary = string.Join(
                "\n",
                apiResponse.Notifications.Select(notification =>
                    $"{notification.Title}: {notification.Delivery}"));
            MessageBox.Show(
                $"Notifications initiated.{overviewSummary}\n\nSent or scheduled: {sentCount}\nFailed: {failedCount}\n\nSchedule:\n{deliverySummary}{failureSummary}",
                "Notifications initiated",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            TrackScheduledNotifications(apiResponse.Notifications);
            SaveWorkspaceState();
            RenderScheduledNotifications();
            ShowToast("Notifications scheduled");
        }
        catch (HttpRequestException ex)
        {
            MessageBox.Show(
                $"The desktop app could not reach the local backend API.\n\n{FormatExceptionMessage(ex)}",
                "Backend connection failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (TaskCanceledException ex)
        {
            MessageBox.Show(
                $"The local backend API took too long to respond.\n\n{FormatExceptionMessage(ex)}",
                "Backend timeout",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The notification could not be sent right now.\n\n{FormatExceptionMessage(ex)}",
                "Notification failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            if (button is not null)
            {
                button.IsEnabled = true;
                button.Content = "End study for now and initiate notifications";
            }
        }
    }

    private async void CancelScheduledNotifications_Click(object sender, RoutedEventArgs e)
    {
        var activeNotifications = _scheduledNotifications
            .Where(notification => !notification.Deleted && !string.IsNullOrWhiteSpace(notification.SequenceId))
            .ToList();

        if (activeNotifications.Count == 0)
        {
            RenderScheduledNotifications();
            ShowToast("No scheduled notifications to cancel");
            return;
        }

        var button = sender as Button;
        if (button is not null)
        {
            button.IsEnabled = false;
            button.Content = "Canceling...";
        }

        try
        {
            var sequenceIds = activeNotifications
                .Select(notification => notification.SequenceId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var response = await CancelScheduledNotificationsThroughBackendAsync(sequenceIds);
            var canceledCount = response.Notifications.Count(notification => notification.Canceled);
            var failedCount = response.Notifications.Count - canceledCount;

            var canceledIds = response.Notifications
                .Where(notification => notification.Canceled)
                .Select(notification => notification.SequenceId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var notification in _scheduledNotifications.Where(notification => canceledIds.Contains(notification.SequenceId)))
            {
                notification.Deleted = true;
                notification.DeletedAt = DateTimeOffset.Now;
            }

            SaveWorkspaceState();
            RenderScheduledNotifications();

            ShowToast(failedCount == 0
                ? $"Canceled {canceledCount} scheduled notifications"
                : $"Canceled {canceledCount}; {failedCount} could not be canceled");
        }
        catch (Exception ex)
        {
            RenderScheduledNotifications();
            MessageBox.Show(
                $"Scheduled notifications could not be canceled right now.\n\n{FormatExceptionMessage(ex)}",
                "Cancel failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void TrackScheduledNotifications(List<NotificationDispatchApiResult> notifications)
    {
        foreach (var notification in notifications.Where(notification =>
                     notification.Sent
                     && !string.Equals(notification.Delivery, "now", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(notification.SequenceId)))
        {
            var existing = _scheduledNotifications.FirstOrDefault(candidate =>
                string.Equals(candidate.SequenceId, notification.SequenceId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.Title = notification.Title;
                existing.Delivery = notification.Delivery;
                existing.Deleted = false;
                existing.DeletedAt = null;
                continue;
            }

            _scheduledNotifications.Add(new ScheduledNotificationState
            {
                SequenceId = notification.SequenceId,
                Title = notification.Title,
                Delivery = notification.Delivery,
                CreatedAt = DateTimeOffset.Now,
                Deleted = false
            });
        }
    }

    private async Task<EndStudySessionApiResponse> EndStudySessionThroughBackendAsync(WorkspaceItem study)
    {
        await EnsureBackendRunningAsync();
        var reminders = CreateReminderApiRequests(study);

        var request = new EndStudySessionApiRequest(
            study.Name,
            CreateStudyReference(study),
            CreateSelectedScriptureText(study),
            OverviewNotificationEnabledCheckBox.IsChecked == true,
            NormalizeOverviewInstruction(OverviewPromptTextBox.Text),
            RemindersEnabledCheckBox.IsChecked == true,
            reminders);

        using var response = await BackendHttpClient.PostAsJsonAsync(
            $"{BackendApiUrl}/api/study-sessions/end",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Backend returned {(int)response.StatusCode}: {error}");
        }

        return await response.Content.ReadFromJsonAsync<EndStudySessionApiResponse>()
            ?? throw new InvalidOperationException("Backend returned an empty response.");
    }

    private async Task<CancelNotificationsApiResponse> CancelScheduledNotificationsThroughBackendAsync(
        List<string> sequenceIds)
    {
        await EnsureBackendRunningAsync();

        using var response = await BackendHttpClient.PostAsJsonAsync(
            $"{BackendApiUrl}/api/notifications/cancel",
            new CancelNotificationsApiRequest(sequenceIds));

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Backend returned {(int)response.StatusCode}: {error}");
        }

        return await response.Content.ReadFromJsonAsync<CancelNotificationsApiResponse>()
            ?? throw new InvalidOperationException("Backend returned an empty response.");
    }

    private void UpdateCancelScheduledNotificationsButton()
    {
        if (CancelScheduledNotificationsButton is null)
        {
            return;
        }

        var activeCount = _scheduledNotifications.Count(notification => !notification.Deleted);
        CancelScheduledNotificationsButton.IsEnabled = activeCount > 0;
        CancelScheduledNotificationsButton.Content = activeCount == 0
            ? "Cancel scheduled"
            : $"Cancel scheduled ({activeCount})";
    }

    private static string FormatExceptionMessage(Exception exception)
    {
        var messages = new List<string>();
        var cursor = exception;
        while (cursor is not null)
        {
            messages.Add(cursor.Message);
            cursor = cursor.InnerException;
        }

        return string.Join("\n", messages.Distinct());
    }

    private static async Task EnsureBackendRunningAsync()
    {
        if (await CanReachBackendAsync())
        {
            return;
        }

        var backendProjectPath = FindBackendProjectPath();
        if (backendProjectPath is null)
        {
            throw new InvalidOperationException("The backend API is not running, and the backend project could not be found.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{backendProjectPath}\"",
            WorkingDirectory = Path.GetDirectoryName(backendProjectPath)!,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(startInfo);

        for (var attempt = 0; attempt < 60; attempt++)
        {
            await Task.Delay(500);
            if (await CanReachBackendAsync())
            {
                return;
            }
        }

        throw new InvalidOperationException("The backend API did not start in time.");
    }

    private static async Task<bool> CanReachBackendAsync()
    {
        try
        {
            using var response = await BackendHttpClient.GetAsync($"{BackendApiUrl}/");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindBackendProjectPath()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null)
        {
            var candidate = Path.Combine(cursor.FullName, "BibleStudy.Backend", "BibleStudy.Backend.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            cursor = cursor.Parent;
        }

        return null;
    }

    private string CreateStudyReference(WorkspaceItem study)
    {
        var root = GetRootWorkspace(study);
        return study.PassageLabel == "No passage selected"
            ? root.Name
            : $"{root.Name} {study.PassageLabel}";
    }

    private string CreateSelectedScriptureText(WorkspaceItem study)
    {
        var root = GetRootWorkspace(study);
        if (study.PassageStartChapter is null || study.PassageStartVerse is null)
        {
            return string.Empty;
        }

        var startChapter = study.PassageStartChapter.Value;
        var startVerse = study.PassageStartVerse.Value;
        var endChapter = study.PassageEndChapter ?? startChapter;
        var endVerse = study.PassageEndVerse ?? startVerse;

        if (endChapter < startChapter || (endChapter == startChapter && endVerse < startVerse))
        {
            (startChapter, endChapter) = (endChapter, startChapter);
            (startVerse, endVerse) = (endVerse, startVerse);
        }

        var selectedVerses = new List<string>();
        for (var chapter = startChapter; chapter <= endChapter; chapter++)
        {
            if (!TryGetChapterVerses(root.Name, chapter, out var verses))
            {
                continue;
            }

            var firstVerse = chapter == startChapter ? startVerse : 1;
            var lastVerse = chapter == endChapter ? endVerse : verses.Count;
            for (var verse = firstVerse; verse <= Math.Min(lastVerse, verses.Count); verse++)
            {
                var text = verses[verse - 1].Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    selectedVerses.Add($"{chapter}:{verse} {text}");
                }
            }
        }

        return string.Join(" ", selectedVerses);
    }

    private List<StudyReminderApiRequest> CreateReminderApiRequests(WorkspaceItem study)
    {
        return _reminderScheduleItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Prompt))
            .Select((item, index) => new StudyReminderApiRequest(
                $"reminder-{index + 1}",
                $"Study reminder {index + 1}",
                NormalizeReminderInstruction(item.Prompt),
                item.NtfyDelay))
            .ToList();
    }

    private void RefreshVerseSelectors(BibleBook? book, int? startChapter, int? endChapter)
    {
        StartVerseSelect.ItemsSource = GetVerseOptions(book, startChapter);
        EndVerseSelect.ItemsSource = GetVerseOptions(book, endChapter);
    }

    private static List<int> GetVerseOptions(BibleBook? book, int? chapter)
    {
        return Enumerable.Range(1, GetVerseCount(book, chapter)).ToList();
    }

    private static int? ClampVerseSelection(BibleBook? book, int? chapter, int? verse)
    {
        if (verse is null || chapter is null)
        {
            return null;
        }

        return Math.Clamp(verse.Value, 1, GetVerseCount(book, chapter));
    }

    private static int GetVerseCount(BibleBook? book, int? chapter)
    {
        if (book is null || chapter is null || chapter < 1 || chapter > book.VerseCounts.Length)
        {
            return 1;
        }

        return book.VerseCounts[chapter.Value - 1];
    }

    private static int? GetSelectedInt(ComboBox comboBox)
    {
        return comboBox.SelectedItem is int value
            ? value
            : null;
    }

    private void MarkCurrentStudyEdited()
    {
        if (_currentStudy is null)
        {
            return;
        }

        _currentStudy.LastEditedAt = DateTime.Now;
        var todayState = GetTodayState();
        var contextKey = CreateStudyContextKey(_currentStudy);
        todayState.ExcludedStudyContextKeys.RemoveAll(key =>
            string.Equals(key, contextKey, StringComparison.OrdinalIgnoreCase));
        RenderTodayDashboard();
        QueueWorkspaceSave();
    }

    private void RestoreContentShell()
    {
        ContentShell.Background = (Brush)FindResource("PanelBackground");
        ContentShell.BorderBrush = (Brush)FindResource("StrokeSoft");
        ContentShell.BorderThickness = new Thickness(1);
        ContentShell.CornerRadius = new CornerRadius(28);
        ContentShell.Padding = new Thickness(24);
    }

    private void FlattenContentShell()
    {
        ContentShell.Background = Brushes.Transparent;
        ContentShell.BorderBrush = Brushes.Transparent;
        ContentShell.BorderThickness = new Thickness(0);
        ContentShell.CornerRadius = new CornerRadius(0);
        ContentShell.Padding = new Thickness(0);
    }

    private void StudyBlock_KeyUp(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not StudyBlock block)
        {
            return;
        }

        if (_isApplyingEditorCommand)
        {
            return;
        }

        block.Text = textBox.Text;
        _activeStudyBlock = block;
        _activeStudyTextBox = textBox;
        MarkCurrentStudyEdited();

        TryAutoCorrectPreviousWord(textBox, block, e.Key);

    }

    private void TryAutoCorrectPreviousWord(TextBox textBox, StudyBlock block, Key key)
    {
        if (textBox.SelectionLength > 0 || !IsWordBoundaryKey(key) || textBox.CaretIndex < 2)
        {
            return;
        }

        var checkIndex = Math.Max(0, textBox.CaretIndex - 2);
        var spellingError = textBox.GetSpellingError(checkIndex);
        if (spellingError is null)
        {
            return;
        }

        var suggestions = spellingError.Suggestions.Cast<string>().Take(2).ToList();
        if (suggestions.Count != 1)
        {
            return;
        }

        var start = textBox.GetSpellingErrorStart(checkIndex);
        var length = textBox.GetSpellingErrorLength(checkIndex);
        if (start < 0 || length <= 0)
        {
            return;
        }

        var suggestion = suggestions[0];
        var oldCaret = textBox.CaretIndex;
        _isApplyingEditorCommand = true;
        try
        {
            textBox.Text = textBox.Text.Remove(start, length).Insert(start, suggestion);
            textBox.CaretIndex = Math.Clamp(oldCaret + suggestion.Length - length, 0, textBox.Text.Length);
            block.Text = textBox.Text;
            MarkCurrentStudyEdited();
        }
        finally
        {
            _isApplyingEditorCommand = false;
        }
    }

    private static bool IsWordBoundaryKey(Key key)
    {
        return key is Key.Space
            or Key.OemPeriod
            or Key.Decimal
            or Key.OemComma
            or Key.OemQuestion
            or Key.OemSemicolon
            or Key.OemQuotes;
    }

    private void StudyBlock_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not StudyBlock block)
        {
            return;
        }

        _activeStudyBlock = block;
        _activeStudyTextBox = textBox;

        if (SlashCommandMenu.Visibility == Visibility.Visible && HandleSlashCommandKeyDown(e))
        {
            return;
        }

        if (e.Key == Key.Back && TryBackspaceAcrossStudyBlocks(textBox, block))
        {
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }

        if (TryHandleInlineListEnter(textBox, block))
        {
            e.Handled = true;
            return;
        }

        InsertBlockAfter(textBox, block);
        e.Handled = true;
    }

    private void StudyBlock_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isApplyingEditorCommand || sender is not TextBox textBox || textBox.Tag is not StudyBlock block)
        {
            return;
        }

        block.Text = textBox.Text;
        _activeStudyBlock = block;
        _activeStudyTextBox = textBox;
        MarkCurrentStudyEdited();
        QueueSmoothEditorCaretUpdate();

        if (textBox.Text.TrimStart().StartsWith("/", StringComparison.Ordinal))
        {
            _maybeInSlashCommand = true;
            UpdateSlashCommandMenu();
            return;
        }

        if (SlashCommandMenu.Visibility == Visibility.Visible || _maybeInSlashCommand)
        {
            HideSlashCommandMenu();
        }
    }

    private void StudyBlock_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not StudyBlock block)
        {
            return;
        }

        _activeStudyBlock = block;
        _activeStudyTextBox = textBox;
        QueueSmoothEditorCaretUpdate();
    }

    private void StudyBlock_CheckChanged(object sender, RoutedEventArgs e)
    {
        MarkCurrentStudyEdited();
    }

    private void StudyBlock_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.Tag is StudyBlock block)
        {
            if (!ReferenceEquals(_activeStudyTextBox, textBox))
            {
                HideSlashCommandMenu();
            }

            if (_activeStudyBlock is not null && !ReferenceEquals(_activeStudyBlock, block))
            {
                _activeStudyBlock.IsFocused = false;
            }

            _activeStudyBlock = block;
            _activeStudyTextBox = textBox;
            block.IsFocused = true;
            QueueSmoothEditorCaretUpdate(animate: false);
        }
    }

    private void StudyBlock_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { Tag: StudyBlock block })
        {
            block.IsFocused = false;
        }

        if (ReferenceEquals(sender, _activeStudyTextBox))
        {
            SmoothEditorCaret.Visibility = Visibility.Collapsed;
        }
    }

    private void QueueSmoothEditorCaretUpdate(bool animate = true)
    {
        Dispatcher.BeginInvoke(() => UpdateSmoothEditorCaret(animate), DispatcherPriority.Render);
    }

    private void UpdateSmoothEditorCaret(bool animate)
    {
        if (_activeStudyTextBox is null
            || !_activeStudyTextBox.IsKeyboardFocusWithin
            || StudyEditorCaretLayer is null
            || SmoothEditorCaret is null)
        {
            if (SmoothEditorCaret is not null)
            {
                SmoothEditorCaret.Visibility = Visibility.Collapsed;
            }

            return;
        }

        var caretIndex = Math.Clamp(_activeStudyTextBox.CaretIndex, 0, _activeStudyTextBox.Text.Length);
        var caretRect = _activeStudyTextBox.GetRectFromCharacterIndex(caretIndex, trailingEdge: true);
        if (caretRect.IsEmpty)
        {
            caretRect = _activeStudyTextBox.GetRectFromCharacterIndex(caretIndex, trailingEdge: false);
        }

        if (caretRect.IsEmpty)
        {
            SmoothEditorCaret.Visibility = Visibility.Collapsed;
            return;
        }

        var target = _activeStudyTextBox.TranslatePoint(new Point(caretRect.X, caretRect.Y), StudyEditorCaretLayer);
        var caretHeight = Math.Max(18, caretRect.Height > 0 ? caretRect.Height : _activeStudyTextBox.FontSize * 1.35);
        SmoothEditorCaret.Height = caretHeight;
        SmoothEditorCaret.Visibility = Visibility.Visible;

        var currentLeft = Canvas.GetLeft(SmoothEditorCaret);
        var currentTop = Canvas.GetTop(SmoothEditorCaret);
        if (!animate || double.IsNaN(currentLeft) || double.IsNaN(currentTop))
        {
            SmoothEditorCaret.BeginAnimation(Canvas.LeftProperty, null);
            SmoothEditorCaret.BeginAnimation(Canvas.TopProperty, null);
            Canvas.SetLeft(SmoothEditorCaret, target.X);
            Canvas.SetTop(SmoothEditorCaret, target.Y);
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(90);
        SmoothEditorCaret.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(target.X, duration)
        {
            EasingFunction = ease
        });
        SmoothEditorCaret.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(target.Y, duration)
        {
            EasingFunction = ease
        });
    }

    private bool HandleSlashCommandKeyDown(KeyEventArgs e)
    {
        if (SlashCommandMenu.Visibility != Visibility.Visible)
        {
            return false;
        }

        if (e.Key == Key.Escape)
        {
            HideSlashCommandMenu();
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Down)
        {
            MoveSlashCommandSelection(1);
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Up)
        {
            MoveSlashCommandSelection(-1);
            e.Handled = true;
            return true;
        }

        if (e.Key is Key.Enter or Key.Tab)
        {
            if (_activeStudyTextBox is not null && _activeStudyBlock is not null)
            {
                _activeStudyBlock.Text = _activeStudyTextBox.Text;
            }

            UpdateSlashCommandMenu();
            if (_filteredSlashCommands.Count > 0)
            {
                ApplySelectedSlashCommand();
            }

            e.Handled = true;
            return true;
        }

        return false;
    }

    private void SlashCommandRefreshTimer_Tick(object? sender, EventArgs e)
    {
        _slashCommandRefreshTimer.Stop();
        UpdateSlashCommandMenu();
    }

    private void SlashCommandList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<ListBoxItem>((DependencyObject)e.OriginalSource) is not { DataContext: EditorCommandOption option })
        {
            return;
        }

        SlashCommandList.SelectedItem = option;
        ApplySelectedSlashCommand();
    }

    private void UpdateSlashCommandMenu()
    {
        var query = GetSlashCommandQuery();
        if (query is null)
        {
            HideSlashCommandMenu();
            return;
        }

        if (query == _lastSlashQuery && SlashCommandMenu.Visibility == Visibility.Visible)
        {
            return;
        }

        _lastSlashQuery = query;
        var matches = _slashCommands
            .Where(command => command.Matches(query))
            .ToList();

        _filteredSlashCommands.Clear();
        foreach (var match in matches)
        {
            _filteredSlashCommands.Add(match);
        }

        if (_filteredSlashCommands.Count == 0)
        {
            HideSlashCommandMenu();
            return;
        }

        SlashCommandList.SelectedIndex = 0;
        ShowSlashCommandMenu();
    }

    private void HideSlashCommandMenu()
    {
        _slashCommandRefreshTimer.Stop();
        if (SlashCommandMenu is not null && SlashCommandMenu.Visibility == Visibility.Visible)
        {
            var fade = new DoubleAnimation(SlashCommandMenu.Opacity, 0, TimeSpan.FromMilliseconds(90))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (_, _) => SlashCommandMenu.Visibility = Visibility.Collapsed;
            SlashCommandMenu.BeginAnimation(OpacityProperty, fade);
            SlashCommandMenuScale?.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 0.96, TimeSpan.FromMilliseconds(90)));
            SlashCommandMenuScale?.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 0.96, TimeSpan.FromMilliseconds(90)));
        }
        _lastSlashQuery = string.Empty;
        _maybeInSlashCommand = false;
        _filteredSlashCommands.Clear();
    }

    private void ShowSlashCommandMenu()
    {
        if (SlashCommandMenu is null || SlashCommandMenuScale is null)
        {
            return;
        }

        var wasVisible = SlashCommandMenu.Visibility == Visibility.Visible;
        SlashCommandMenu.Visibility = Visibility.Visible;
        if (wasVisible)
        {
            SlashCommandMenu.Opacity = 1;
            return;
        }

        SlashCommandMenu.Opacity = 0;
        SlashCommandMenuScale.ScaleX = 0.96;
        SlashCommandMenuScale.ScaleY = 0.96;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        SlashCommandMenu.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = ease
        });
        SlashCommandMenuScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = ease
        });
        SlashCommandMenuScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = ease
        });
    }

    private void MoveSlashCommandSelection(int direction)
    {
        if (_filteredSlashCommands.Count == 0)
        {
            return;
        }

        var selectedIndex = SlashCommandList.SelectedIndex < 0
            ? 0
            : SlashCommandList.SelectedIndex;
        selectedIndex = (selectedIndex + direction + _filteredSlashCommands.Count) % _filteredSlashCommands.Count;
        SlashCommandList.SelectedIndex = selectedIndex;
        SlashCommandList.ScrollIntoView(_filteredSlashCommands[selectedIndex]);
    }

    private void ApplySelectedSlashCommand()
    {
        if (SlashCommandList.SelectedItem is EditorCommandOption option)
        {
            ApplyBlockCommand(option.Kind);
        }
    }

    private void ApplyBlockCommand(EditorBlockKind blockKind)
    {
        if (_activeStudyBlock is null || _activeStudyTextBox is null)
        {
            return;
        }

        _isApplyingEditorCommand = true;
        try
        {
            var startingText = GetInitialTextForBlock(blockKind);
            _activeStudyBlock.Text = startingText;
            _activeStudyBlock.Kind = blockKind;
            _activeStudyBlock.IsChecked = false;
            _activeStudyTextBox.Text = startingText;
            _activeStudyTextBox.CaretIndex = startingText.Length;
            UpdateNumberedListLabels();
            MarkCurrentStudyEdited();
        }
        finally
        {
            _isApplyingEditorCommand = false;
        }

        HideSlashCommandMenu();
        _activeStudyTextBox.Focus();
    }

    private void InsertBlockAfter(TextBox textBox, StudyBlock block)
    {
        if (_currentStudy is null)
        {
            return;
        }

        var selectionStart = Math.Min(textBox.SelectionStart, textBox.Text.Length);
        var selectionEnd = Math.Min(selectionStart + textBox.SelectionLength, textBox.Text.Length);
        var currentText = textBox.Text[..selectionStart];
        var nextText = textBox.Text[selectionEnd..];
        if (IsContinuableListBlock(block.Kind) && string.IsNullOrWhiteSpace(textBox.Text))
        {
            _isApplyingEditorCommand = true;
            try
            {
                block.Kind = EditorBlockKind.Normal;
                block.Text = string.Empty;
                block.IsChecked = false;
                textBox.Text = string.Empty;
                textBox.CaretIndex = 0;
                UpdateNumberedListLabels();
                MarkCurrentStudyEdited();
            }
            finally
            {
                _isApplyingEditorCommand = false;
            }

            HideSlashCommandMenu();
            FocusBlock(block, 0);
            return;
        }

        var nextBlock = CreateStudyBlock(IsContinuableListBlock(block.Kind)
            ? block.Kind
            : EditorBlockKind.Normal);
        nextBlock.Text = nextText;

        _isApplyingEditorCommand = true;
        try
        {
            block.Text = currentText;
            textBox.Text = currentText;
            textBox.CaretIndex = currentText.Length;

            var blockIndex = _currentStudy.Blocks.IndexOf(block);
            if (blockIndex < 0)
            {
                _currentStudy.Blocks.Add(nextBlock);
            }
            else
            {
                _currentStudy.Blocks.Insert(blockIndex + 1, nextBlock);
            }

            UpdateNumberedListLabels();
            MarkCurrentStudyEdited();
        }
        finally
        {
            _isApplyingEditorCommand = false;
        }

        HideSlashCommandMenu();
        FocusBlock(nextBlock, 0);
    }

    private static bool IsContinuableListBlock(EditorBlockKind kind)
    {
        return kind is EditorBlockKind.BulletedList or EditorBlockKind.NumberedList or EditorBlockKind.Todo;
    }

    private bool TryHandleInlineListEnter(TextBox textBox, StudyBlock block)
    {
        if (_currentStudy is null || !IsInlineListBlock(block.Kind))
        {
            return false;
        }

        var text = textBox.Text;
        var selectionStart = Math.Min(textBox.SelectionStart, text.Length);
        var selectionEnd = Math.Min(selectionStart + textBox.SelectionLength, text.Length);
        if (selectionEnd > selectionStart)
        {
            text = text.Remove(selectionStart, selectionEnd - selectionStart);
            textBox.Text = text;
            textBox.CaretIndex = selectionStart;
        }

        var lineStart = text.LastIndexOf('\n', Math.Max(0, selectionStart - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var lineEnd = text.IndexOf('\n', selectionStart);
        lineEnd = lineEnd < 0 ? text.Length : lineEnd;
        var currentLine = text[lineStart..lineEnd].TrimEnd('\r');
        var currentItemText = StripInlineListPrefix(currentLine, block.Kind).Trim();

        if (string.IsNullOrWhiteSpace(currentItemText))
        {
            ExitInlineListAtCurrentLine(textBox, block, lineStart, lineEnd);
            return true;
        }

        var nextPrefix = GetNextInlineListPrefix(text, selectionStart, block.Kind);
        var insertion = Environment.NewLine + nextPrefix;
        textBox.Text = text.Insert(selectionStart, insertion);
        textBox.CaretIndex = selectionStart + insertion.Length;
        block.Text = textBox.Text;
        MarkCurrentStudyEdited();
        HideSlashCommandMenu();
        return true;
    }

    private void ExitInlineListAtCurrentLine(TextBox textBox, StudyBlock block, int lineStart, int lineEnd)
    {
        if (_currentStudy is null)
        {
            return;
        }

        var text = textBox.Text;
        var removeStart = lineStart > 0 ? lineStart - 1 : lineStart;
        var removeEnd = lineEnd;
        if (removeEnd < text.Length && text[removeEnd] == '\r')
        {
            removeEnd++;
        }

        if (removeEnd < text.Length && text[removeEnd] == '\n')
        {
            removeEnd++;
        }

        var remainingText = text.Remove(removeStart, removeEnd - removeStart).TrimEnd('\r', '\n');
        if (string.IsNullOrWhiteSpace(remainingText))
        {
            _isApplyingEditorCommand = true;
            try
            {
                block.Kind = EditorBlockKind.Normal;
                block.Text = string.Empty;
                textBox.Text = string.Empty;
                textBox.CaretIndex = 0;
                MarkCurrentStudyEdited();
            }
            finally
            {
                _isApplyingEditorCommand = false;
            }

            HideSlashCommandMenu();
            FocusBlock(block, 0);
            return;
        }

        var nextBlock = CreateNormalStudyBlock();
        var blockIndex = _currentStudy.Blocks.IndexOf(block);
        _isApplyingEditorCommand = true;
        try
        {
            block.Text = remainingText;
            textBox.Text = remainingText;
            textBox.CaretIndex = textBox.Text.Length;
            if (blockIndex < 0)
            {
                _currentStudy.Blocks.Add(nextBlock);
            }
            else
            {
                _currentStudy.Blocks.Insert(blockIndex + 1, nextBlock);
            }

            UpdateNumberedListLabels();
            MarkCurrentStudyEdited();
        }
        finally
        {
            _isApplyingEditorCommand = false;
        }

        HideSlashCommandMenu();
        FocusBlock(nextBlock, 0);
    }

    private static bool IsInlineListBlock(EditorBlockKind kind)
    {
        return kind is EditorBlockKind.BulletedList or EditorBlockKind.NumberedList;
    }

    private static string StripInlineListPrefix(string line, EditorBlockKind kind)
    {
        if (kind == EditorBlockKind.BulletedList && line.TrimStart().StartsWith("\u2022", StringComparison.Ordinal))
        {
            var bulletIndex = line.IndexOf('\u2022');
            return line[(bulletIndex + 1)..].TrimStart();
        }

        if (kind == EditorBlockKind.NumberedList)
        {
            var trimmed = line.TrimStart();
            var dotIndex = trimmed.IndexOf('.');
            if (dotIndex > 0 && trimmed[..dotIndex].All(char.IsDigit))
            {
                return trimmed[(dotIndex + 1)..].TrimStart();
            }
        }

        return line;
    }

    private static string GetNextInlineListPrefix(string text, int insertionIndex, EditorBlockKind kind)
    {
        if (kind == EditorBlockKind.BulletedList)
        {
            return "\u2022 ";
        }

        var linesBeforeCaret = text[..Math.Min(insertionIndex, text.Length)]
            .Split('\n')
            .Count(line => line.TrimStart().TakeWhile(char.IsDigit).Any());
        return $"{Math.Max(1, linesBeforeCaret + 1)}. ";
    }

    private bool TryBackspaceAcrossStudyBlocks(TextBox textBox, StudyBlock block)
    {
        if (_currentStudy is null
            || textBox.SelectionLength > 0
            || textBox.CaretIndex != 0)
        {
            return false;
        }

        var blockIndex = _currentStudy.Blocks.IndexOf(block);
        if (blockIndex <= 0)
        {
            return false;
        }

        var previousBlock = _currentStudy.Blocks[blockIndex - 1];
        var previousCaretIndex = previousBlock.Text.Length;
        var currentText = textBox.Text;

        _isApplyingEditorCommand = true;
        try
        {
            if (!string.IsNullOrEmpty(currentText))
            {
                previousBlock.Text += currentText;
            }

            _currentStudy.Blocks.RemoveAt(blockIndex);
            UpdateNumberedListLabels();
            MarkCurrentStudyEdited();
        }
        finally
        {
            _isApplyingEditorCommand = false;
        }

        HideSlashCommandMenu();
        FocusBlock(previousBlock, previousCaretIndex);
        return true;
    }

    private void QueueSlashCommandRefresh()
    {
        _slashCommandRefreshTimer.Stop();
        _slashCommandRefreshTimer.Start();
    }

    private static string GetInitialTextForBlock(EditorBlockKind blockKind)
    {
        return blockKind switch
        {
            EditorBlockKind.BulletedList => "\u2022 ",
            EditorBlockKind.NumberedList => "1. ",
            _ => string.Empty
        };
    }

    private string? GetSlashCommandQuery()
    {
        var paragraphText = _activeStudyTextBox?.Text.TrimStart() ?? string.Empty;
        if (!paragraphText.StartsWith("/", StringComparison.Ordinal))
        {
            return null;
        }

        var query = paragraphText[1..].Trim();
        return query.Contains(' ', StringComparison.Ordinal)
            ? null
            : query;
    }

    private void EnsureStudyHasBlocks(WorkspaceItem study)
    {
        if (study.Blocks.Count == 0)
        {
            study.Blocks.Add(CreateNormalStudyBlock());
        }
    }

    private StudyBlock CreateNormalStudyBlock()
    {
        return CreateStudyBlock(EditorBlockKind.Normal);
    }

    private StudyBlock CreateStudyBlock(EditorBlockKind kind)
    {
        return new StudyBlock(
            kind,
            PickEditorBrush("TextPrimary"),
            PickEditorBrush("TextSecondary"),
            PickEditorBrush("Mint"),
            PickEditorBrush("PanelBackground"),
            PickEditorBrush("AppBackground"),
            PickEditorBrush("Coral"));
    }

    private void RefreshStudyBlockThemeBrushes()
    {
        foreach (var study in GetAllStudies())
        {
            foreach (var block in study.Blocks)
            {
                block.UpdateThemeBrushes(
                    PickEditorBrush("TextPrimary"),
                    PickEditorBrush("TextSecondary"),
                    PickEditorBrush("Mint"),
                    PickEditorBrush("PanelBackground"),
                    PickEditorBrush("AppBackground"),
                    PickEditorBrush("Coral"));
            }
        }
    }

    private void UpdateNumberedListLabels()
    {
        if (_currentStudy is null)
        {
            return;
        }

        var listIndex = 1;
        foreach (var block in _currentStudy.Blocks)
        {
            if (block.Kind == EditorBlockKind.NumberedList)
            {
                block.NumberLabel = $"{listIndex}.";
                listIndex++;
                continue;
            }

            listIndex = 1;
        }
    }

    private void FocusBlock(StudyBlock block, int? caretIndex = null)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var container = StudyBlockItems.ItemContainerGenerator.ContainerFromItem(block) as DependencyObject;
            var textBox = container is null ? null : FindChild<TextBox>(container);
            textBox?.Focus();
            if (textBox is not null)
            {
                textBox.CaretIndex = Math.Clamp(caretIndex ?? textBox.Text.Length, 0, textBox.Text.Length);
            }
        }, DispatcherPriority.Background);
    }

    private Brush PickEditorBrush(string resourceName)
    {
        return GetResourceBrush(resourceName);
    }

    private Brush GetResourceBrush(string resourceName)
    {
        return TryFindResource(resourceName) as Brush
            ?? new SolidColorBrush(Color.FromRgb(254, 250, 224));
    }

    private static List<WorkspaceItem> GetContainerLineage(WorkspaceItem item)
    {
        var lineage = new List<WorkspaceItem>();
        var cursor = item;

        while (cursor is not null)
        {
            lineage.Add(cursor);
            cursor = cursor.Parent;
        }

        lineage.Reverse();
        return lineage;
    }

    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        var targetWidth = _sidebarCollapsed ? 64 : 284;
        var targetPadding = _sidebarCollapsed ? new Thickness(8, 22, 8, 22) : new Thickness(22);
        AnimateSidebarWidth(SidebarColumn.ActualWidth > 0 ? SidebarColumn.ActualWidth : SidebarColumn.Width.Value, targetWidth);
        SidebarRoot.BeginAnimation(Border.PaddingProperty, new ThicknessAnimation(SidebarRoot.Padding, targetPadding, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        });
        SidebarToggleButton.HorizontalAlignment = _sidebarCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        SidebarToggleButton.ToolTip = _sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar";

        var menuVisibility = _sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        BibleStudyNavButton.Visibility = menuVisibility;
        TodayNavButton.Visibility = menuVisibility;
        SettingsNavButton.Visibility = menuVisibility;
        PrayerBoardNavButton.Visibility = menuVisibility;
        ReadingPlanNavButton.Visibility = menuVisibility;
        ResourceLibraryNavButton.Visibility = menuVisibility;
        NavTabIndicator.Visibility = Visibility.Collapsed;
    }

    private void AnimateSidebarWidth(double fromWidth, double toWidth)
    {
        SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
        SidebarColumn.Width = new GridLength(Math.Max(0, fromWidth));
        var animation = new GridLengthAnimation
        {
            From = new GridLength(Math.Max(0, fromWidth)),
            To = new GridLength(Math.Max(0, toWidth)),
            Duration = new Duration(TimeSpan.FromMilliseconds(240)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        animation.Completed += (_, _) =>
        {
            SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            SidebarColumn.Width = new GridLength(toWidth);
            if (!_sidebarCollapsed)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    NavTabIndicator.Visibility = Visibility.Visible;
                    PositionNavTabIndicator(GetActiveNavButton(_activeNavTab));
                }, DispatcherPriority.ContextIdle);
            }
        };
        SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, animation);
    }

    private void ScripturePanelToggle_Click(object sender, RoutedEventArgs e)
    {
        _scripturePanelHidden = !_scripturePanelHidden;

        if (_scripturePanelHidden)
        {
            if (ScriptureColumn.ActualWidth > 0)
            {
                _scripturePanelVisibleWidth = new GridLength(ScriptureColumn.ActualWidth);
            }

            StopScriptureSmoothScroll();
            HideStrongsPanel(animate: true);
            ScriptureColumn.MinWidth = 0;
            ScripturePanelRoot.BeginAnimation(OpacityProperty, null);
            AnimateScripturePanelCollapseThenClose(ScriptureColumn.ActualWidth);
            ScripturePanelToggleButton.ToolTip = "Show scripture panel";
            ScripturePanelToggleButtonText.Text = "Show";
            ScripturePanelShowButtonText.Text = "Show";
            return;
        }

        var targetWidth = _scripturePanelVisibleWidth.Value <= 0
            ? 440
            : Math.Max(280, _scripturePanelVisibleWidth.Value);
        ScriptureColumn.MinWidth = 0;
        ScriptureColumnSplitter.Visibility = Visibility.Visible;
        ScripturePanelRoot.Visibility = Visibility.Collapsed;
        ScripturePanelRoot.Opacity = 1;
        ScripturePanelScale.ScaleX = 0.04;
        ScripturePanelScale.ScaleY = 0.04;
        ScripturePanelRotate.Angle = -5;
        ScripturePanelShowButton.Visibility = Visibility.Collapsed;
        AnimateScripturePanelWidth(ScriptureColumn.ActualWidth, targetWidth, hiding: false);
        ScripturePanelToggleButton.ToolTip = "Hide scripture panel";
        ScripturePanelToggleButtonText.Text = "Hide";
        ScripturePanelShowButtonText.Text = "Show";

        if (_currentStudy is not null)
        {
            RenderScripturePanel(_currentStudy, _scriptureVisibleChapter, scrollToSelection: false);
        }
    }

    private void StrongsToggle_Changed(object sender, RoutedEventArgs e)
    {
        _strongsEnabled = StrongsToggleButton.IsChecked == true;
        if (!_strongsEnabled)
        {
            HideStrongsPanel(animate: true);
        }
        else
        {
            HideStrongsHoverPreview();
            ScriptureSelectionRail.Visibility = Visibility.Collapsed;
            ScriptureSelectionMarker.Visibility = Visibility.Collapsed;
        }

        RenderScriptureDocument();
        if (!_strongsEnabled)
        {
            UpdateScriptureSelectionMarker();
        }
    }

    private void StrongsPhrase_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Hyperlink { Tag: StrongsSelection selection })
        {
            return;
        }

        e.Handled = true;
        ShowStrongsPanel(selection);
    }

    private void StrongsLink_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Hyperlink { Tag: StrongsSelection selection })
        {
            return;
        }

        ShowStrongsHoverPreview(selection);
    }

    private void StrongsLink_MouseLeave(object sender, MouseEventArgs e)
    {
        HideStrongsHoverPreview();
    }

    private void CloseStrongsPanel_Click(object sender, RoutedEventArgs e)
    {
        HideStrongsPanel(animate: true);
    }

    private void ShowStrongsHoverPreview(StrongsSelection selection)
    {
        var entry = selection.Entry;
        var greekText = !string.IsNullOrWhiteSpace(entry.Greek)
            ? entry.Greek
            : selection.Phrase;

        StrongsHoverGreekText.Text = greekText;
        StrongsHoverStrongText.Text = string.IsNullOrWhiteSpace(entry.DStrong)
            ? string.Empty
            : entry.DStrong;
        StrongsHoverGlossText.Text = FirstNonEmpty(entry.Gloss, entry.SubMeaning, entry.DictionaryForm);
        StrongsHoverTranslationText.Text = string.IsNullOrWhiteSpace(entry.English)
            ? selection.Phrase
            : $"Translation: {entry.English}";

        StrongsHoverPopup.IsOpen = true;
        StrongsHoverPopupRoot.BeginAnimation(OpacityProperty, null);
        StrongsHoverPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        StrongsHoverPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        StrongsHoverPopupRoot.Opacity = 0;
        StrongsHoverPopupScale.ScaleX = 0.72;
        StrongsHoverPopupScale.ScaleY = 0.72;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        StrongsHoverPopupRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130))
        {
            EasingFunction = ease
        });
        StrongsHoverPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.72, 1, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = ease
        });
        StrongsHoverPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.72, 1, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = ease
        });
    }

    private void HideStrongsHoverPreview()
    {
        if (!StrongsHoverPopup.IsOpen)
        {
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(StrongsHoverPopupRoot.Opacity, 0, TimeSpan.FromMilliseconds(90))
        {
            EasingFunction = ease
        };
        fade.Completed += (_, _) => StrongsHoverPopup.IsOpen = false;
        StrongsHoverPopupRoot.BeginAnimation(OpacityProperty, fade);
        StrongsHoverPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(StrongsHoverPopupScale.ScaleX, 0.86, TimeSpan.FromMilliseconds(90))
        {
            EasingFunction = ease
        });
        StrongsHoverPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(StrongsHoverPopupScale.ScaleY, 0.86, TimeSpan.FromMilliseconds(90))
        {
            EasingFunction = ease
        });
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private void ShowStrongsPanel(StrongsSelection selection)
    {
        var entry = selection.Entry;
        var lexiconEntry = FindGreekLexiconEntry(entry);
        StrongsPanelReferenceText.Text = $"{selection.BookName} {selection.Chapter}:{selection.Verse}";
        StrongsGreekText.Text = !string.IsNullOrWhiteSpace(lexiconEntry?.Greek)
            ? $"{entry.Greek}\n{lexiconEntry.Greek}"
            : string.IsNullOrWhiteSpace(entry.Greek)
            ? selection.Phrase
            : entry.Greek;
        StrongsEnglishText.Text = string.IsNullOrWhiteSpace(entry.English)
            ? selection.Phrase
            : entry.English;
        StrongsNumberText.Text = string.IsNullOrWhiteSpace(entry.DStrong)
            ? "Strong's: available when the full TAGNT dStrong row is loaded."
            : $"Strong's: {entry.DStrong}";
        StrongsGrammarText.Text = BuildTagntDetailLine(entry);

        var lexiconSummary = new[]
        {
            string.IsNullOrWhiteSpace(lexiconEntry?.Transliteration) ? null : $"Transliteration: {lexiconEntry.Transliteration}",
            string.IsNullOrWhiteSpace(lexiconEntry?.Morph) ? null : $"Morph: {lexiconEntry.Morph}",
            string.IsNullOrWhiteSpace(lexiconEntry?.Gloss) ? null : $"Gloss: {lexiconEntry.Gloss}",
            string.IsNullOrWhiteSpace(entry.DictionaryForm) ? null : $"TAGNT form: {entry.DictionaryForm}",
            string.IsNullOrWhiteSpace(entry.Gloss) ? null : $"TAGNT gloss: {entry.Gloss}",
            string.IsNullOrWhiteSpace(entry.SubMeaning) ? null : $"Context: {entry.SubMeaning}"
        }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        StrongsLexiconSummaryText.Text = string.Join("\n", lexiconSummary);

        SetStrongsDefinitionDocument(lexiconEntry?.Meaning);
        UpdateStrongsSectionVisibility();

        if (StrongsPanelRoot.Visibility != Visibility.Visible)
        {
            StrongsPanelRoot.Visibility = Visibility.Visible;
            StrongsPanelRoot.Opacity = 0;
        }

        var targetWidth = _strongsPanelVisibleWidth.Value <= 0
            ? 330
            : Math.Clamp(_strongsPanelVisibleWidth.Value, 220, 520);
        AnimateStrongsPanelWidth(StrongsColumn.ActualWidth, targetWidth, show: true);
    }

    private void UpdateStrongsSectionVisibility()
    {
        StrongsWordSection.Visibility = HasVisibleText(StrongsGreekText) || HasVisibleText(StrongsEnglishText)
            ? Visibility.Visible
            : Visibility.Collapsed;
        StrongsNumberSection.Visibility = HasVisibleText(StrongsNumberText)
            ? Visibility.Visible
            : Visibility.Collapsed;
        StrongsTagntSection.Visibility = HasVisibleText(StrongsGrammarText)
            ? Visibility.Visible
            : Visibility.Collapsed;
        StrongsLexiconSummarySection.Visibility = HasVisibleText(StrongsLexiconSummaryText)
            ? Visibility.Visible
            : Visibility.Collapsed;
        StrongsDefinitionSection.Visibility = StrongsDefinitionDocument.Blocks.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static bool HasVisibleText(TextBlock textBlock)
    {
        return !string.IsNullOrWhiteSpace(textBlock.Text);
    }

    private void SetStrongsDefinitionDocument(string? meaning)
    {
        StrongsDefinitionDocument.Blocks.Clear();

        var definitionText = string.IsNullOrWhiteSpace(meaning)
            ? "Definition lookup is ready. Drop the TBESG Greek lexicon text file into the app Data folder to show the full Strong's definition here."
            : meaning;

        var lines = BuildDefinitionLines(definitionText);
        if (lines.Count == 0)
        {
            return;
        }

        var hasMainPoint = false;
        foreach (var line in lines)
        {
            if (line.Kind == DefinitionLineKind.Main)
            {
                if (hasMainPoint || StrongsDefinitionDocument.Blocks.Count > 0)
                {
                    AddDefinitionDivider();
                }

                hasMainPoint = true;
            }

            StrongsDefinitionDocument.Blocks.Add(CreateDefinitionParagraph(line));
        }
    }

    private Paragraph CreateDefinitionParagraph(DefinitionLine line)
    {
        var paragraph = new Paragraph
        {
            Margin = line.Kind switch
            {
                DefinitionLineKind.Main => new Thickness(0, 0, 0, 8),
                DefinitionLineKind.Sub => new Thickness(34, 0, 0, 7),
                _ => new Thickness(0, 0, 0, 7)
            },
            LineHeight = line.Kind == DefinitionLineKind.Main ? 22 : 20,
            Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 17))
        };

        if (!string.IsNullOrWhiteSpace(line.Marker))
        {
            paragraph.Inlines.Add(new Run(line.Marker)
            {
                FontSize = line.Kind == DefinitionLineKind.Main ? 18 : 15,
                FontWeight = FontWeights.Black,
                Foreground = GetResourceBrush(line.Kind == DefinitionLineKind.Main ? "PanelBackground" : "AppBackground")
            });
            paragraph.Inlines.Add(new Run(" "));
        }

        AddDefinitionTextRuns(paragraph, line.Text, line.Kind != DefinitionLineKind.Lead);
        return paragraph;
    }

    private void AddDefinitionDivider()
    {
        StrongsDefinitionDocument.Blocks.Add(new BlockUIContainer(new Border
        {
            Height = 1,
            Margin = new Thickness(0, 5, 0, 9),
            Background = GetResourceBrush("PanelBackground"),
            Opacity = 0.45
        }));
    }

    private static void AddDefinitionTextRuns(Paragraph paragraph, string text, bool boldLeadPhrase)
    {
        var trimmed = text.Trim();
        if (!boldLeadPhrase)
        {
            paragraph.Inlines.Add(new Run(trimmed)
            {
                FontSize = 15,
                FontWeight = FontWeights.SemiBold
            });
            return;
        }

        var leadLength = FindDefinitionLeadPhraseLength(trimmed);
        if (leadLength <= 0)
        {
            paragraph.Inlines.Add(new Run(trimmed)
            {
                FontSize = 15
            });
            return;
        }

        paragraph.Inlines.Add(new Run(trimmed[..leadLength])
        {
            FontSize = paragraph.LineHeight >= 22 ? 16 : 15,
            FontWeight = FontWeights.Black
        });

        if (leadLength < trimmed.Length)
        {
            paragraph.Inlines.Add(new Run(trimmed[leadLength..])
            {
                FontSize = paragraph.LineHeight >= 22 ? 15 : 14
            });
        }
    }

    private static int FindDefinitionLeadPhraseLength(string text)
    {
        var delimiterIndex = text.IndexOfAny([',', ';', ':']);
        if (delimiterIndex < 0 || delimiterIndex > 72)
        {
            return 0;
        }

        return delimiterIndex + 1;
    }

    private static List<DefinitionLine> BuildDefinitionLines(string meaning)
    {
        var lines = new List<DefinitionLine>();
        if (string.IsNullOrWhiteSpace(meaning))
        {
            return lines;
        }

        var normalized = meaning.Trim();
        normalized = Regex.Replace(normalized, @"\s*__([IVXLCDM]+)\.\s*", "\n$1. ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\s*__([0-9]+)\.\s*", "\n$1. ", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\s*__\(([a-z])\)\s*", "\n$1. ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"(?<!\n)(?<![A-Za-z])\s([0-9]+)\.\s+", "\n$1. ", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"(?<!\n)\s([a-z])\.\s+", "\n$1. ", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n", RegexOptions.CultureInvariant);

        foreach (var rawLine in normalized.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = Regex.Replace(rawLine.Trim(), @"\s+", " ", RegexOptions.CultureInvariant);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var romanMatch = Regex.Match(line, @"^(?<marker>[IVXLCDM]+)\.\s*(?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (romanMatch.Success)
            {
                lines.Add(new DefinitionLine(DefinitionLineKind.Main, "-", $"{romanMatch.Groups["marker"].Value}. {romanMatch.Groups["text"].Value.Trim()}"));
                continue;
            }

            var numberMatch = Regex.Match(line, @"^(?<marker>[0-9]+)\.\s*(?<text>.+)$", RegexOptions.CultureInvariant);
            if (numberMatch.Success)
            {
                lines.Add(new DefinitionLine(DefinitionLineKind.Main, "-", $"{numberMatch.Groups["marker"].Value}. {numberMatch.Groups["text"].Value.Trim()}"));
                continue;
            }

            var letterMatch = Regex.Match(line, @"^(?<marker>[a-z])\.\s*(?<text>.+)$", RegexOptions.CultureInvariant);
            if (letterMatch.Success)
            {
                lines.Add(new DefinitionLine(DefinitionLineKind.Sub, "↳", $"{letterMatch.Groups["marker"].Value}. {letterMatch.Groups["text"].Value.Trim()}"));
                continue;
            }

            lines.Add(new DefinitionLine(DefinitionLineKind.Lead, string.Empty, line));
        }

        return lines;
    }

    private static string BuildTagntDetailLine(TagntWordEntry entry)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(entry.Grammar) ? null : $"Grammar: {entry.Grammar}",
            string.IsNullOrWhiteSpace(entry.WordType) ? null : $"Type: {entry.WordType}",
            string.IsNullOrWhiteSpace(entry.Editions) ? null : $"Editions: {entry.Editions}",
            string.IsNullOrWhiteSpace(entry.SimpleStrong) ? null : $"Simple Strong's: {entry.SimpleStrong}",
            string.IsNullOrWhiteSpace(entry.AltStrongs) ? null : $"Alt Strong's: {entry.AltStrongs}",
            string.IsNullOrWhiteSpace(entry.ConjoinedWord) ? null : $"Conjoined: {entry.ConjoinedWord}",
            string.IsNullOrWhiteSpace(entry.VariantNotes) ? null : $"Variants: {entry.VariantNotes}"
        }.Where(part => !string.IsNullOrWhiteSpace(part));

        var detail = string.Join("\n", parts);
        return string.IsNullOrWhiteSpace(detail)
            ? "Grammar and variant details will appear when those TAGNT rows are loaded."
            : detail;
    }

    private void PanelDragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement handle || StudyPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        var kind = ReferenceEquals(handle, ScripturePanelDragHandle)
            ? MovablePanelKind.Scripture
            : ReferenceEquals(handle, StrongsPanelDragHandle)
                ? MovablePanelKind.Strongs
                : (MovablePanelKind?)null;
        if (kind is null || kind == MovablePanelKind.Strongs && StrongsPanelRoot.Visibility != Visibility.Visible)
        {
            return;
        }

        _draggingPanelKind = kind;
        _draggingPanelHandle = handle;
        _draggingPanelTransform = kind == MovablePanelKind.Scripture
            ? ScripturePanelDragTransform
            : StrongsPanelDragTransform;
        _draggingPanelTransform.BeginAnimation(TranslateTransform.XProperty, null);
        _draggingPanelTransform.BeginAnimation(TranslateTransform.YProperty, null);
        _panelDragStartPoint = e.GetPosition(StudyPanel);
        _panelDragPointer = _panelDragStartPoint;
        _panelDragStartOffset = new Point(_draggingPanelTransform.X, _draggingPanelTransform.Y);
        _panelDragPendingOffset = _panelDragStartOffset;
        _hoveredPanelPreset = null;
        Panel.SetZIndex(GetPanelRoot(kind.Value), 300);
        ShowPanelPresetOverlay();
        handle.CaptureMouse();
        Mouse.OverrideCursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void PanelDragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingPanelKind is null
            || _draggingPanelTransform is null
            || !ReferenceEquals(sender, _draggingPanelHandle)
            || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var pointer = e.GetPosition(StudyPanel);
        _panelDragPointer = pointer;
        var delta = pointer - _panelDragStartPoint;
        _panelDragPendingOffset = new Point(
            _panelDragStartOffset.X + delta.X,
            _panelDragStartOffset.Y + delta.Y);
        if (!_panelDragRenderSubscribed)
        {
            _panelDragRenderSubscribed = true;
            CompositionTarget.Rendering += PanelDrag_Rendering;
        }

        e.Handled = true;
    }

    private void PanelDrag_Rendering(object? sender, EventArgs e)
    {
        if (_draggingPanelKind is null || _draggingPanelTransform is null)
        {
            StopPanelDragRendering();
            return;
        }

        _draggingPanelTransform.X = _panelDragPendingOffset.X;
        _draggingPanelTransform.Y = _panelDragPendingOffset.Y;
        SetPanelPresetHighlight(_hoveredPanelPreset ?? GetNearestPanelPreset());
    }

    private void PanelDragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingPanelKind is null || !ReferenceEquals(sender, _draggingPanelHandle))
        {
            return;
        }

        var kind = _draggingPanelKind.Value;
        var preset = _hoveredPanelPreset ?? GetNearestPanelPreset();
        StopPanelDragRendering();
        if (_draggingPanelHandle?.IsMouseCaptured == true)
        {
            _draggingPanelHandle.ReleaseMouseCapture();
        }

        Mouse.OverrideCursor = null;
        _draggingPanelKind = null;
        _draggingPanelHandle = null;
        _draggingPanelTransform = null;
        Panel.SetZIndex(GetPanelRoot(kind), kind == MovablePanelKind.Strongs ? 120 : 110);
        HidePanelPresetOverlay();
        ApplyPanelPreset(kind, preset, animate: true);
        e.Handled = true;
    }

    private void StopPanelDragRendering()
    {
        if (_panelDragRenderSubscribed)
        {
            CompositionTarget.Rendering -= PanelDrag_Rendering;
            _panelDragRenderSubscribed = false;
        }
    }

    private void ShowPanelPresetOverlay()
    {
        if (PanelPresetOverlay is null)
        {
            return;
        }

        PanelPresetOverlay.Visibility = Visibility.Visible;
        PanelPresetOverlay.IsHitTestVisible = true;
        PositionPanelPresetTargets();
        SetPanelPresetHighlight(GetNearestPanelPreset());
    }

    private void HidePanelPresetOverlay()
    {
        if (PanelPresetOverlay is null)
        {
            return;
        }

        PanelPresetOverlay.Visibility = Visibility.Collapsed;
        PanelPresetOverlay.IsHitTestVisible = false;
        _hoveredPanelPreset = null;
        foreach (var target in new[] { PanelPresetTop, PanelPresetLeft, PanelPresetRight, PanelPresetBottom, PanelPresetFloat })
        {
            target.Background = PanelPresetIdleBrush;
            target.BorderBrush = PanelPresetIdleBorderBrush;
        }
    }

    private void PositionPanelPresetTargets()
    {
        var width = Math.Max(360, StudyPanel.ActualWidth);
        var height = Math.Max(260, StudyPanel.ActualHeight);
        const double targetWidth = 128;
        const double targetHeight = 42;
        const double edge = 14;
        Canvas.SetLeft(PanelPresetTop, (width - targetWidth) / 2);
        Canvas.SetTop(PanelPresetTop, edge);
        Canvas.SetLeft(PanelPresetBottom, (width - targetWidth) / 2);
        Canvas.SetTop(PanelPresetBottom, Math.Max(edge, height - targetHeight - edge));
        Canvas.SetLeft(PanelPresetLeft, edge);
        Canvas.SetTop(PanelPresetLeft, (height - targetHeight) / 2);
        Canvas.SetLeft(PanelPresetRight, Math.Max(edge, width - targetWidth - edge));
        Canvas.SetTop(PanelPresetRight, (height - targetHeight) / 2);
        Canvas.SetLeft(PanelPresetFloat, (width - targetWidth) / 2);
        Canvas.SetTop(PanelPresetFloat, Math.Max(edge, (height - targetHeight) / 2 - targetHeight - 12));
    }

    private void PanelPreset_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border { Tag: string tag } && Enum.TryParse<PanelPreset>(tag, out var preset))
        {
            _hoveredPanelPreset = preset;
            SetPanelPresetHighlight(preset);
        }
    }

    private void PanelPreset_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_draggingPanelKind is not null)
        {
            SetPanelPresetHighlight(_hoveredPanelPreset ?? GetNearestPanelPreset());
        }
    }

    private void PanelPreset_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingPanelKind is null
            || sender is not Border { Tag: string tag }
            || !Enum.TryParse<PanelPreset>(tag, out var preset))
        {
            return;
        }

        _hoveredPanelPreset = preset;
        PanelDragHandle_MouseLeftButtonUp(_draggingPanelHandle!, e);
        e.Handled = true;
    }

    private void SetPanelPresetHighlight(PanelPreset? preset)
    {
        foreach (var target in new[] { PanelPresetTop, PanelPresetLeft, PanelPresetRight, PanelPresetBottom, PanelPresetFloat })
        {
            var isSelected = preset.HasValue
                && string.Equals(target.Tag as string, preset.Value.ToString(), StringComparison.OrdinalIgnoreCase);
            target.Background = isSelected ? PanelPresetActiveBrush : PanelPresetIdleBrush;
            target.BorderBrush = isSelected ? PanelPresetActiveBorderBrush : PanelPresetIdleBorderBrush;
            target.Opacity = isSelected ? 1 : 0.84;
        }
    }

    private PanelPreset GetNearestPanelPreset()
    {
        if (_draggingPanelKind is null)
        {
            return PanelPreset.Right;
        }

        var width = Math.Max(1, StudyPanel.ActualWidth);
        var height = Math.Max(1, StudyPanel.ActualHeight);
        var pointer = _panelDragPointer;
        if (pointer.Y < height * 0.20)
        {
            return PanelPreset.Top;
        }

        if (pointer.Y > height * 0.80)
        {
            return PanelPreset.Bottom;
        }

        if (pointer.X < width * 0.20)
        {
            return PanelPreset.Left;
        }

        if (pointer.X > width * 0.80)
        {
            return PanelPreset.Right;
        }

        return PanelPreset.Float;
    }

    private Border GetPanelRoot(MovablePanelKind kind)
    {
        return kind == MovablePanelKind.Scripture ? ScripturePanelRoot : StrongsPanelRoot;
    }

    private TranslateTransform GetPanelTransform(MovablePanelKind kind)
    {
        return kind == MovablePanelKind.Scripture ? ScripturePanelDragTransform : StrongsPanelDragTransform;
    }

    private Rect GetPanelBounds(Border panel)
    {
        if (panel.ActualWidth <= 0 || panel.ActualHeight <= 0 || StudyPanel.ActualWidth <= 0)
        {
            return Rect.Empty;
        }

        var transform = panel.TransformToAncestor(StudyPanel);
        var topLeft = transform.Transform(new Point(0, 0));
        var bottomRight = transform.Transform(new Point(panel.ActualWidth, panel.ActualHeight));
        return new Rect(topLeft, bottomRight);
    }

    private void ApplyPanelPreset(MovablePanelKind kind, PanelPreset preset, bool animate)
    {
        var panel = GetPanelRoot(kind);
        var transform = GetPanelTransform(kind);
        if (panel.ActualWidth <= 0 || panel.ActualHeight <= 0)
        {
            return;
        }

        ConfigurePanelLayoutForPreset(panel, preset);
        StudyPanel.UpdateLayout();
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        var currentBounds = GetPanelBounds(panel);
        var desired = GetPresetTopLeft(preset, currentBounds);
        var targetX = transform.X + desired.X - currentBounds.Left;
        var targetY = transform.Y + desired.Y - currentBounds.Top;
        if (animate)
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(230));
            var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };
            transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(transform.X, targetX, duration)
            {
                EasingFunction = ease
            });
            var yAnimation = new DoubleAnimation(transform.Y, targetY, duration)
            {
                EasingFunction = ease
            };
            yAnimation.Completed += (_, _) => UpdateEditorAvoidanceForPanels(animate: false);
            transform.BeginAnimation(TranslateTransform.YProperty, yAnimation);
        }
        else
        {
            transform.X = targetX;
            transform.Y = targetY;
        }

        if (kind == MovablePanelKind.Scripture)
        {
            _scripturePanelPreset = preset;
        }
        else
        {
            _strongsPanelPreset = preset;
        }

        Dispatcher.BeginInvoke(() => UpdateEditorAvoidanceForPanels(animate), DispatcherPriority.Render);
    }

    private Point GetPresetTopLeft(PanelPreset preset, Rect currentBounds)
    {
        var width = Math.Max(1, StudyPanel.ActualWidth);
        var height = Math.Max(1, StudyPanel.ActualHeight);
        const double edge = 14;
        return preset switch
        {
            PanelPreset.Top => new Point(Math.Max(edge, (width - currentBounds.Width) / 2), edge),
            PanelPreset.Left => new Point(edge, 0),
            PanelPreset.Bottom => new Point(Math.Max(edge, (width - currentBounds.Width) / 2), Math.Max(edge, height - currentBounds.Height - edge)),
            PanelPreset.Float => new Point(Math.Max(edge, (width - currentBounds.Width) / 2), Math.Max(edge, (height - currentBounds.Height) / 2)),
            _ => new Point(Math.Max(edge, width - currentBounds.Width - edge), 0)
        };
    }

    private void ConfigurePanelLayoutForPreset(Border panel, PanelPreset preset)
    {
        var height = Math.Max(260, StudyPanel.ActualHeight);
        switch (preset)
        {
            case PanelPreset.Top:
            case PanelPreset.Bottom:
                panel.Height = Math.Max(260, height * 0.46);
                panel.VerticalAlignment = preset == PanelPreset.Top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
                break;
            case PanelPreset.Float:
                panel.Height = Math.Max(260, height * 0.58);
                panel.VerticalAlignment = VerticalAlignment.Top;
                break;
            default:
                panel.Height = double.NaN;
                panel.VerticalAlignment = VerticalAlignment.Stretch;
                break;
        }
    }

    private void UpdateEditorAvoidanceForPanels(bool animate)
    {
        if (StudyEditorScrollViewer is null || StudyPanel.ActualWidth <= 0)
        {
            return;
        }

        var editorWidth = Math.Max(0, StudyEditorColumn.ActualWidth);
        var leftAvoidance = 0d;
        var rightAvoidance = 0d;
        foreach (var panel in new[] { ScripturePanelRoot, StrongsPanelRoot })
        {
            if (panel.Visibility != Visibility.Visible || panel.ActualWidth <= 0)
            {
                continue;
            }

            var bounds = GetPanelBounds(panel);
            if (bounds == Rect.Empty || bounds.Bottom < 0 || bounds.Top > StudyPanel.ActualHeight)
            {
                continue;
            }

            var overlapLeft = Math.Max(0, Math.Min(editorWidth, bounds.Right) - Math.Max(0, bounds.Left));
            if (overlapLeft <= 1)
            {
                continue;
            }

            if (bounds.Left + (bounds.Width / 2) < editorWidth / 2)
            {
                leftAvoidance = Math.Max(leftAvoidance, Math.Min(editorWidth - 120, bounds.Right + 10));
            }
            else
            {
                rightAvoidance = Math.Max(rightAvoidance, Math.Min(editorWidth - 120, editorWidth - bounds.Left + 10));
            }
        }

        var targetMargin = new Thickness(4 + leftAvoidance, 0, 24 + rightAvoidance, 0);
        var targetCaretMargin = new Thickness(4 + leftAvoidance, 0, 24 + rightAvoidance, 0);
        if (!animate)
        {
            StudyEditorScrollViewer.Margin = targetMargin;
            StudyEditorCaretLayer.Margin = targetCaretMargin;
            return;
        }

        StudyEditorScrollViewer.BeginAnimation(FrameworkElement.MarginProperty, new ThicknessAnimation
        {
            To = targetMargin,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        StudyEditorCaretLayer.BeginAnimation(FrameworkElement.MarginProperty, new ThicknessAnimation
        {
            To = targetCaretMargin,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void StudyPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        PositionPanelPresetTargets();
        if (_draggingPanelKind is null)
        {
            UpdateEditorAvoidanceForPanels(animate: false);
        }
    }

    private void HideStrongsPanel(bool animate)
    {
        if (StrongsColumn.ActualWidth <= 0 && StrongsPanelRoot.Visibility != Visibility.Visible)
        {
            return;
        }

        StopStrongsSmoothScroll();
        if (StrongsColumn.ActualWidth > 0)
        {
            _strongsPanelVisibleWidth = new GridLength(StrongsColumn.ActualWidth);
        }

        if (!animate)
        {
            StrongsColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            StrongsColumn.Width = new GridLength(0);
            StrongsSplitterColumn.Width = new GridLength(0);
            StrongsColumnSplitter.Visibility = Visibility.Collapsed;
            StrongsPanelRoot.BeginAnimation(OpacityProperty, null);
            StrongsPanelRoot.Opacity = 0;
            StrongsPanelRoot.Visibility = Visibility.Collapsed;
            UpdateEditorAvoidanceForPanels(animate: false);
            return;
        }

        AnimateStrongsPanelWidth(StrongsColumn.ActualWidth, 0, show: false);
    }

    private void AnimateStrongsPanelWidth(double fromWidth, double toWidth, bool show)
    {
        var duration = TimeSpan.FromMilliseconds(show ? 190 : 150);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        if (show)
        {
            StrongsSplitterColumn.Width = new GridLength(8);
            StrongsColumnSplitter.Visibility = Visibility.Visible;
            StrongsColumn.MinWidth = 220;
        }

        StrongsColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
        StrongsColumn.Width = new GridLength(Math.Max(0, fromWidth));
        var widthAnimation = new GridLengthAnimation
        {
            From = new GridLength(Math.Max(0, fromWidth)),
            To = new GridLength(Math.Max(0, toWidth)),
            Duration = new Duration(duration),
            EasingFunction = ease
        };
        widthAnimation.Completed += (_, _) =>
        {
            StrongsColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            StrongsColumn.Width = new GridLength(Math.Max(0, toWidth));
            if (!show)
            {
                StrongsColumn.MinWidth = 0;
                StrongsSplitterColumn.Width = new GridLength(0);
                StrongsColumnSplitter.Visibility = Visibility.Collapsed;
                StrongsPanelRoot.Visibility = Visibility.Collapsed;
                StrongsPanelRoot.Opacity = 0;
                UpdateEditorAvoidanceForPanels(animate: true);
            }
        };

        StrongsColumn.BeginAnimation(ColumnDefinition.WidthProperty, widthAnimation);
        StrongsPanelRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 0 : 1, show ? 1 : 0, duration)
        {
            EasingFunction = ease
        });
    }

    private void StrongsColumnSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (StrongsColumn.ActualWidth > 0)
        {
            _strongsPanelVisibleWidth = new GridLength(StrongsColumn.ActualWidth);
        }
    }

    private void AnimateScripturePanelCollapseThenClose(double fromWidth)
    {
        var duration = TimeSpan.FromMilliseconds(180);
        var ease = new BackEase
        {
            EasingMode = EasingMode.EaseIn,
            Amplitude = 0.35
        };

        var scaleX = new DoubleAnimation(1, 0.04, duration) { EasingFunction = ease };
        var scaleY = new DoubleAnimation(1, 0.04, duration) { EasingFunction = ease };
        var rotate = new DoubleAnimation(0, -5, duration) { EasingFunction = ease };
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        scaleX.Completed += (_, _) =>
        {
            ScripturePanelRoot.Visibility = Visibility.Collapsed;
            ScripturePanelRoot.Opacity = 1;
            ScripturePanelScale.ScaleX = 0.04;
            ScripturePanelScale.ScaleY = 0.04;
            ScripturePanelRotate.Angle = -5;
            AnimateScripturePanelWidth(fromWidth, 0, hiding: true);
        };

        ScripturePanelScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        ScripturePanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        ScripturePanelRotate.BeginAnimation(RotateTransform.AngleProperty, rotate);
        ScripturePanelRoot.BeginAnimation(OpacityProperty, fade);
    }

    private void AnimateScripturePanelWidth(double fromWidth, double toWidth, bool hiding)
    {
        var duration = TimeSpan.FromMilliseconds(hiding ? 150 : 210);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        ScriptureColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
        ScriptureColumn.Width = new GridLength(Math.Max(0, fromWidth));
        var widthAnimation = new GridLengthAnimation
        {
            From = new GridLength(Math.Max(0, fromWidth)),
            To = new GridLength(Math.Max(0, toWidth)),
            Duration = new Duration(duration),
            EasingFunction = ease
        };
        widthAnimation.Completed += (_, _) =>
        {
            ScriptureColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            ScriptureColumn.Width = new GridLength(Math.Max(0, toWidth));
            ScriptureColumnSplitter.BeginAnimation(FrameworkElement.WidthProperty, null);
            if (hiding)
            {
                ScriptureColumnSplitter.Width = 0;
                ScriptureColumnSplitter.Visibility = Visibility.Collapsed;
                ScripturePanelRoot.Visibility = Visibility.Collapsed;
                ScripturePanelRoot.Opacity = 1;
                ScripturePanelShowButton.Visibility = Visibility.Visible;
                UpdateEditorAvoidanceForPanels(animate: true);
            }
            else
            {
                ScriptureColumn.MinWidth = 280;
                ScriptureColumnSplitter.Width = 10;
                ScriptureColumnSplitter.Visibility = Visibility.Visible;
                ScripturePanelRoot.Visibility = Visibility.Visible;
                ScripturePanelRoot.Opacity = 0;
                ScripturePanelScale.ScaleX = 0.04;
                ScripturePanelScale.ScaleY = 0.04;
                ScripturePanelRotate.Angle = -5;
                var revealEase = new BackEase
                {
                    EasingMode = EasingMode.EaseOut,
                    Amplitude = 0.28
                };
                ScripturePanelScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.04, 1, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = revealEase
                });
                ScripturePanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.04, 1, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = revealEase
                });
                ScripturePanelRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-5, 0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                ScripturePanelRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                Dispatcher.BeginInvoke(() => UpdateEditorAvoidanceForPanels(animate: false), DispatcherPriority.Render);
            }
        };

        ScriptureColumn.BeginAnimation(ColumnDefinition.WidthProperty, widthAnimation);
        ScriptureColumnSplitter.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(
            hiding ? 10 : 0,
            hiding ? 0 : 10,
            duration)
        {
            EasingFunction = ease
        });
    }

    private void ScriptureColumnSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_scripturePanelHidden && ScriptureColumn.ActualWidth > 0)
        {
            _scripturePanelVisibleWidth = new GridLength(ScriptureColumn.ActualWidth);
        }
    }

    private Brush PickAccentBrush(int offset)
    {
        var selectedBookOffset = _selectedBook is null
            ? 0
            : BibleBooks.IndexOf(_selectedBook);
        return _accentPalette[(selectedBookOffset + offset + 1) % _accentPalette.Length];
    }

    private static string CreateUniqueName(string baseName, IEnumerable<WorkspaceItem> siblings)
    {
        var siblingNames = siblings.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!siblingNames.Contains(baseName))
        {
            return baseName;
        }

        var index = 2;
        while (siblingNames.Contains($"{baseName} {index}"))
        {
            index++;
        }

        return $"{baseName} {index}";
    }

    private static Brush BrushFrom(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }

    private static T? FindParent<T>(DependencyObject source)
        where T : DependencyObject
    {
        var cursor = source;
        while (cursor is not null)
        {
            if (cursor is T match)
            {
                return match;
            }

            cursor = VisualTreeHelper.GetParent(cursor);
        }

        return null;
    }

    private static T? FindChild<T>(DependencyObject source)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(source); index++)
        {
            var child = VisualTreeHelper.GetChild(source, index);
            if (child is T match)
            {
                return match;
            }

            var nestedMatch = FindChild<T>(child);
            if (nestedMatch is not null)
            {
                return nestedMatch;
            }
        }

        return null;
    }

    private TextBox? FindWorkspaceRenameTextBox(WorkspaceItem item)
    {
        var container = WorkspaceItems.ItemContainerGenerator.ContainerFromItem(item) as DependencyObject;
        return container is null
            ? null
            : FindChildren<TextBox>(container).FirstOrDefault(textBox => ReferenceEquals(textBox.Tag, item));
    }

    private static IEnumerable<T> FindChildren<T>(DependencyObject source)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(source); index++)
        {
            var child = VisualTreeHelper.GetChild(source, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nestedMatch in FindChildren<T>(child))
            {
                yield return nestedMatch;
            }
        }
    }
}

public sealed record BibleBook(string Name, string Testament, int[] VerseCounts, Brush AccentBrush)
{
    public int Chapters => VerseCounts.Length;

    public string ChapterLabel => Chapters == 1 ? "1 chapter" : $"{Chapters} chapters";
}

public sealed class WorkspaceItem : INotifyPropertyChanged
{
    private string _name;
    private int? _passageStartChapter;
    private int? _passageStartVerse;
    private int? _passageEndChapter;
    private int? _passageEndVerse;
    private DateTime? _lastEditedAt;
    private bool _isRenaming;

    public WorkspaceItem(string name, WorkspaceItemKind kind, Brush accentBrush, WorkspaceItem? parent)
    {
        _name = name;
        Kind = kind;
        AccentBrush = accentBrush;
        Parent = parent;
    }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public bool IsRenaming
    {
        get => _isRenaming;
        private set
        {
            if (_isRenaming == value)
            {
                return;
            }

            _isRenaming = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRenaming)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayNameVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RenameTextBoxVisibility)));
        }
    }

    public string RenameOriginalName { get; private set; } = string.Empty;

    public Visibility DisplayNameVisibility => IsRenaming
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility RenameTextBoxVisibility => IsRenaming
        ? Visibility.Visible
        : Visibility.Collapsed;

    public WorkspaceItemKind Kind { get; }

    public Brush AccentBrush { get; }

    public WorkspaceItem? Parent { get; }

    public ObservableCollection<WorkspaceItem> Children { get; } = new();

    public ObservableCollection<StudyBlock> Blocks { get; } = new();

    public int? PassageStartChapter
    {
        get => _passageStartChapter;
        set
        {
            if (_passageStartChapter == value)
            {
                return;
            }

            _passageStartChapter = value;
            NotifyPassageChanged();
        }
    }

    public int? PassageStartVerse
    {
        get => _passageStartVerse;
        set
        {
            if (_passageStartVerse == value)
            {
                return;
            }

            _passageStartVerse = value;
            NotifyPassageChanged();
        }
    }

    public int? PassageEndChapter
    {
        get => _passageEndChapter;
        set
        {
            if (_passageEndChapter == value)
            {
                return;
            }

            _passageEndChapter = value;
            NotifyPassageChanged();
        }
    }

    public int? PassageEndVerse
    {
        get => _passageEndVerse;
        set
        {
            if (_passageEndVerse == value)
            {
                return;
            }

            _passageEndVerse = value;
            NotifyPassageChanged();
        }
    }

    public DateTime? LastEditedAt
    {
        get => _lastEditedAt;
        set
        {
            if (_lastEditedAt == value)
            {
                return;
            }

            _lastEditedAt = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastEditedAt)));
        }
    }

    public string KindLabel => Kind switch
    {
        WorkspaceItemKind.Book => "Book",
        WorkspaceItemKind.Folder => "Folder",
        _ => "Study"
    };

    public string DetailLabel => Kind switch
    {
        WorkspaceItemKind.Folder => Children.Count == 1 ? "1 item" : $"{Children.Count} items",
        WorkspaceItemKind.Study => PassageLabel,
        _ => string.Empty
    };

    public string PassageLabel
    {
        get
        {
            if (PassageStartChapter is null || PassageStartVerse is null)
            {
                return "No passage selected";
            }

            if (PassageEndChapter is null || PassageEndVerse is null)
            {
                return $"{PassageStartChapter}:{PassageStartVerse}";
            }

            if (PassageStartChapter == PassageEndChapter && PassageStartVerse == PassageEndVerse)
            {
                return $"{PassageStartChapter}:{PassageStartVerse}";
            }

            return $"{PassageStartChapter}:{PassageStartVerse}-{PassageEndChapter}:{PassageEndVerse}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshDetail()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DetailLabel)));
    }

    public void BeginRename()
    {
        RenameOriginalName = Name;
        IsRenaming = true;
    }

    public void EndRename()
    {
        RenameOriginalName = string.Empty;
        IsRenaming = false;
    }

    public void CancelRename()
    {
        if (!string.IsNullOrWhiteSpace(RenameOriginalName))
        {
            Name = RenameOriginalName;
        }

        EndRename();
    }

    private void NotifyPassageChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PassageStartChapter)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PassageStartVerse)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PassageEndChapter)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PassageEndVerse)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PassageLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DetailLabel)));
    }
}

public enum WorkspaceItemKind
{
    Book,
    Folder,
    Study
}

public enum BreadcrumbTarget
{
    Library,
    Today,
    ReminderSettings,
    Settings,
    ColorThemeSettings,
    WorkspaceItem,
    Study
}

public enum AppNavTab
{
    BibleStudy,
    Today,
    ReminderSettings,
    Settings
}

public enum EditorBlockKind
{
    Normal,
    HeadingOne,
    HeadingTwo,
    HeadingThree,
    Quote,
    BulletedList,
    NumberedList,
    Todo,
    Callout,
    Code
}

public sealed class StudyBlock : INotifyPropertyChanged
{
    private Brush _normalBrush;
    private Brush _secondaryBrush;
    private Brush _accentBrush;
    private Brush _panelBrush;
    private Brush _appBrush;
    private Brush _coralBrush;
    private string _text = string.Empty;
    private EditorBlockKind _kind;
    private bool _isFocused;
    private bool _isChecked;
    private string _numberLabel = "1.";

    public StudyBlock(
        EditorBlockKind kind,
        Brush normalBrush,
        Brush secondaryBrush,
        Brush accentBrush,
        Brush panelBrush,
        Brush appBrush,
        Brush coralBrush)
    {
        _kind = kind;
        _normalBrush = normalBrush;
        _secondaryBrush = secondaryBrush;
        _accentBrush = accentBrush;
        _panelBrush = panelBrush;
        _appBrush = appBrush;
        _coralBrush = coralBrush;
    }

    public void UpdateThemeBrushes(
        Brush normalBrush,
        Brush secondaryBrush,
        Brush accentBrush,
        Brush panelBrush,
        Brush appBrush,
        Brush coralBrush)
    {
        _normalBrush = normalBrush;
        _secondaryBrush = secondaryBrush;
        _accentBrush = accentBrush;
        _panelBrush = panelBrush;
        _appBrush = appBrush;
        _coralBrush = coralBrush;
        NotifyStyleChanged();
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value)
            {
                return;
            }

            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused == value)
            {
                return;
            }

            _isFocused = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderBrush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderThickness)));
        }
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }

            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public string NumberLabel
    {
        get => _numberLabel;
        set
        {
            if (_numberLabel == value)
            {
                return;
            }

            _numberLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NumberLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkerText)));
        }
    }

    public EditorBlockKind Kind
    {
        get => _kind;
        set
        {
            if (_kind == value)
            {
                return;
            }

            _kind = value;
            NotifyStyleChanged();
        }
    }

    public double FontSize => Kind switch
    {
        EditorBlockKind.HeadingOne => 34,
        EditorBlockKind.HeadingTwo => 25,
        EditorBlockKind.HeadingThree => 20,
        EditorBlockKind.Quote => 17,
        EditorBlockKind.Code => 15,
        _ => 16
    };

    public FontWeight FontWeight => Kind switch
    {
        EditorBlockKind.HeadingOne => FontWeights.Black,
        EditorBlockKind.HeadingTwo => FontWeights.Bold,
        EditorBlockKind.HeadingThree => FontWeights.Bold,
        _ => FontWeights.Normal
    };

    public FontStyle FontStyle => Kind == EditorBlockKind.Quote
        ? FontStyles.Italic
        : FontStyles.Normal;

    public FontFamily FontFamily => Kind == EditorBlockKind.Code
        ? new FontFamily("Consolas")
        : new FontFamily("Segoe UI");

    public Brush ForegroundBrush => _normalBrush;

    public Brush BackgroundBrush => Kind switch
    {
        EditorBlockKind.Quote => _panelBrush,
        EditorBlockKind.Callout => _panelBrush,
        EditorBlockKind.Code => _appBrush,
        _ => IsFocused
            ? _panelBrush
            : Brushes.Transparent
    };

    public Brush BorderBrush => Kind switch
    {
        EditorBlockKind.Quote => _accentBrush,
        EditorBlockKind.Callout => _accentBrush,
        EditorBlockKind.Code => _coralBrush,
        _ => Brushes.Transparent
    };

    public Thickness BorderThickness => Kind switch
    {
        EditorBlockKind.Quote => new Thickness(4, 0, 0, 0),
        EditorBlockKind.Callout => new Thickness(1),
        EditorBlockKind.Code => new Thickness(1),
        _ => new Thickness(0)
    };

    public Thickness Padding => Kind switch
    {
        EditorBlockKind.Quote => new Thickness(14, 8, 0, 8),
        EditorBlockKind.Callout => new Thickness(14, 10, 14, 10),
        EditorBlockKind.Code => new Thickness(14, 10, 14, 10),
        _ => new Thickness(10, 6, 10, 6)
    };

    public Thickness Margin => Kind switch
    {
        EditorBlockKind.HeadingOne => new Thickness(0, 10, 0, 16),
        EditorBlockKind.HeadingTwo => new Thickness(0, 8, 0, 14),
        EditorBlockKind.HeadingThree => new Thickness(0, 6, 0, 12),
        EditorBlockKind.Quote => new Thickness(0, 8, 0, 16),
        EditorBlockKind.Callout => new Thickness(0, 8, 0, 14),
        EditorBlockKind.Code => new Thickness(0, 8, 0, 14),
        _ => new Thickness(0, 0, 0, 14)
    };

    public double MinHeight => Kind switch
    {
        EditorBlockKind.HeadingOne => 46,
        EditorBlockKind.HeadingTwo => 36,
        EditorBlockKind.HeadingThree => 32,
        EditorBlockKind.Quote => 44,
        EditorBlockKind.Callout => 44,
        EditorBlockKind.Code => 44,
        _ => 28
    };

    public double TextMinHeight => Math.Max(22, MinHeight - Padding.Top - Padding.Bottom);

    public GridLength MarkerWidth => Kind switch
    {
        EditorBlockKind.Todo => new GridLength(30),
        EditorBlockKind.Callout => new GridLength(30),
        _ => new GridLength(0)
    };

    public string MarkerText => Kind switch
    {
        EditorBlockKind.Callout => "!",
        _ => string.Empty
    };

    public Visibility MarkerVisibility => Kind == EditorBlockKind.Callout
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility CheckboxVisibility => Kind == EditorBlockKind.Todo
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Brush MarkerBrush => _normalBrush;

    public double MarkerFontSize => Kind == EditorBlockKind.BulletedList
        ? 20
        : 16;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyStyleChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontSize)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontWeight)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontStyle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontFamily)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ForegroundBrush)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderBrush)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderThickness)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Padding)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Margin)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MinHeight)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextMinHeight)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkerWidth)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkerText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkerVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CheckboxVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkerBrush)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkerFontSize)));
    }
}

public sealed record EditorCommandOption(
    string Title,
    string Description,
    string Shortcut,
    string SearchTerms,
    EditorBlockKind Kind,
    Brush AccentBrush)
{
    public bool Matches(string query)
    {
        return string.IsNullOrWhiteSpace(query)
            || SearchTerms.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Title.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record BreadcrumbPayload(BreadcrumbTarget Target, object? Value);

public sealed class GridLengthAnimation : AnimationTimeline
{
    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From),
        typeof(GridLength),
        typeof(GridLengthAnimation));

    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To),
        typeof(GridLength),
        typeof(GridLengthAnimation));

    public GridLength From
    {
        get => (GridLength)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction { get; set; }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore()
    {
        return new GridLengthAnimation();
    }

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        var progress = animationClock.CurrentProgress ?? 0;
        if (EasingFunction is not null)
        {
            progress = EasingFunction.Ease(progress);
        }

        var from = From.Value;
        var to = To.Value;
        return new GridLength(from + ((to - from) * progress), GridUnitType.Pixel);
    }
}

public sealed class WorkspaceState
{
    public List<WorkspaceItemState> Books { get; set; } = new();

    public List<DailyNoteState> DailyNotes { get; set; } = new();

    public List<ScheduledNotificationState> ScheduledNotifications { get; set; } = new();

    public bool OldTestamentBooksExpanded { get; set; } = true;

    public bool NewTestamentBooksExpanded { get; set; } = true;

    public ReminderSettingsState ReminderSettings { get; set; } = new();

    public ColorSettingsState ColorSettings { get; set; } = new();
}

public sealed class ColorSettingsState
{
    public string SelectedSchemeName { get; set; } = "Olive Study";

    public List<ColorSchemePreset> Presets { get; set; } = new();
}

public sealed record ColorSchemePreset(
    string Name,
    string AppBackground,
    string SidebarBackground,
    string PanelBackground,
    string Accent,
    string TextPrimary,
    string TextSecondary,
    bool BuiltIn)
{
    [JsonIgnore]
    public Brush AppBackgroundBrush => BrushFromHex(AppBackground);

    [JsonIgnore]
    public Brush SidebarBackgroundBrush => BrushFromHex(SidebarBackground);

    [JsonIgnore]
    public Brush PanelBackgroundBrush => BrushFromHex(PanelBackground);

    [JsonIgnore]
    public Brush AccentBrush => BrushFromHex(Accent);

    [JsonIgnore]
    public Brush TextPrimaryBrush => BrushFromHex(TextPrimary);

    [JsonIgnore]
    public Brush TextSecondaryBrush => BrushFromHex(TextSecondary);

    private static Brush BrushFromHex(string hex)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex.Trim()));
        }
        catch
        {
            return Brushes.Transparent;
        }
    }
}

public sealed class WorkspaceItemState
{
    public string Name { get; set; } = string.Empty;

    public WorkspaceItemKind Kind { get; set; }

    public string AccentHex { get; set; } = "#FFFFFFFF";

    public int? PassageStartChapter { get; set; }

    public int? PassageStartVerse { get; set; }

    public int? PassageEndChapter { get; set; }

    public int? PassageEndVerse { get; set; }

    public DateTime? LastEditedAt { get; set; }

    public List<WorkspaceItemState> Children { get; set; } = new();

    public List<StudyBlockState> Blocks { get; set; } = new();
}

public sealed class StudyBlockState
{
    public EditorBlockKind Kind { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool IsChecked { get; set; }
}

public sealed class DailyNoteState
{
    public string DateKey { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public List<string> ExcludedStudyContextKeys { get; set; } = new();
}

public sealed class ScheduledNotificationState
{
    public string SequenceId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Delivery { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public bool Deleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class ReminderSettingsState
{
    public bool OverviewEnabled { get; set; } = true;

    public string OverviewPrompt { get; set; } = string.Empty;

    public bool RemindersEnabled { get; set; } = true;

    public List<ReminderScheduleItemState> Reminders { get; set; } = new();

    public List<ReminderPresetState> Presets { get; set; } = new();
}

public sealed class ReminderPresetState
{
    public string Name { get; set; } = string.Empty;

    public List<ReminderScheduleItemState> Reminders { get; set; } = new();
}

public sealed class ReminderScheduleItemState
{
    public int DelayAmount { get; set; } = 1;

    public string DelayUnit { get; set; } = "hours";

    public string Prompt { get; set; } = string.Empty;
}

public sealed class ReminderPreset
{
    public ReminderPreset(string name, List<ReminderScheduleItem> reminders)
    {
        Name = name;
        Reminders = reminders;
    }

    public string Name { get; }

    public List<ReminderScheduleItem> Reminders { get; }
}

public sealed class ReminderScheduleItem : INotifyPropertyChanged
{
    private int _delayAmount;
    private string _delayUnit;
    private string _prompt;

    public ReminderScheduleItem(int delayAmount, string delayUnit, string prompt)
    {
        _delayAmount = Math.Max(1, delayAmount);
        _delayUnit = delayUnit;
        _prompt = prompt;
    }

    public int DelayAmount
    {
        get => _delayAmount;
        set
        {
            var nextValue = Math.Max(1, value);
            if (_delayAmount == nextValue)
            {
                return;
            }

            _delayAmount = nextValue;
            NotifyChanged();
        }
    }

    public string DelayUnit
    {
        get => _delayUnit;
        set
        {
            var nextValue = value;
            if (_delayUnit == nextValue)
            {
                return;
            }

            _delayUnit = nextValue;
            NotifyChanged();
        }
    }

    public string Prompt
    {
        get => _prompt;
        set
        {
            if (_prompt == value)
            {
                return;
            }

            _prompt = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Prompt)));
        }
    }

    public string DelayLabel => $"After {DelayAmount} {DelayUnit}";

    public string NtfyDelay => DelayUnit switch
    {
        "minutes" => DelayAmount == 1 ? "1 minute" : $"{DelayAmount} minutes",
        "days" => DelayAmount == 1 ? "1 day" : $"{DelayAmount} days",
        _ => DelayAmount == 1 ? "1 hour" : $"{DelayAmount} hours"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReminderScheduleItem Clone()
    {
        return new ReminderScheduleItem(DelayAmount, DelayUnit, Prompt);
    }

    private void NotifyChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DelayAmount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DelayUnit)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DelayLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NtfyDelay)));
    }
}

public sealed record DailyStudyEntry(
    WorkspaceItem Study,
    string StudyName,
    string LocationLabel,
    string PassageLabel,
    string LastEditedLabel,
    string ContextKey);

public sealed class ScheduledNotificationDisplay
{
    public ScheduledNotificationDisplay(ScheduledNotificationState state)
    {
        SequenceId = state.SequenceId;
        Title = string.IsNullOrWhiteSpace(state.Title)
            ? "Scheduled message"
            : state.Title;
        Detail = $"{state.Delivery} - {state.SequenceId}";
        StatusLabel = state.Deleted ? "Deleted" : "Active";
        StatusBrush = state.Deleted
            ? new SolidColorBrush(Color.FromRgb(214, 201, 157))
            : new SolidColorBrush(Color.FromRgb(221, 161, 94));
        RemoveVisibility = state.Deleted
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public string SequenceId { get; }

    public string Title { get; }

    public string Detail { get; }

    public string StatusLabel { get; }

    public Brush StatusBrush { get; }

    public Visibility RemoveVisibility { get; }
}

public sealed class ScriptureVerseDisplay
{
    public ScriptureVerseDisplay(int verseNumber, string text, bool isSelected)
    {
        VerseNumber = verseNumber;
        Text = text;
        IsSelected = isSelected;
    }

    public int VerseNumber { get; }

    public string Text { get; }

    public string DisplayText => $"{VerseNumber} {Text}";

    public bool IsSelected { get; }

    public Brush BackgroundBrush => IsSelected
        ? new SolidColorBrush(Color.FromRgb(221, 161, 94))
        : Brushes.Transparent;

    public Brush BorderBrush => IsSelected
        ? new SolidColorBrush(Color.FromRgb(17, 17, 17))
        : Brushes.Transparent;

    public Thickness BorderThickness => IsSelected
        ? new Thickness(1)
        : new Thickness(0);
}

public sealed record ScriptureDataSource(string Label, string Path);

public readonly record struct ScriptureReferenceKey(string BookName, int Chapter, int Verse);

public sealed class TagntWordEntry
{
    public TagntWordEntry(int wordIndex, string greek)
    {
        WordIndex = wordIndex;
        Greek = greek;
    }

    public int WordIndex { get; }

    public string Greek { get; set; }

    public string English { get; set; } = string.Empty;

    public string DStrong { get; set; } = string.Empty;

    public string Grammar { get; set; } = string.Empty;

    public string DictionaryForm { get; set; } = string.Empty;

    public string Gloss { get; set; } = string.Empty;

    public string WordType { get; set; } = string.Empty;

    public string Editions { get; set; } = string.Empty;

    public string VariantNotes { get; set; } = string.Empty;

    public string SubMeaning { get; set; } = string.Empty;

    public string ConjoinedWord { get; set; } = string.Empty;

    public string SimpleStrong { get; set; } = string.Empty;

    public string AltStrongs { get; set; } = string.Empty;
}

public sealed record TagntTextMatch(int Start, int Length, TagntWordEntry Entry);

public sealed record StrongsSelection(
    string BookName,
    int Chapter,
    int Verse,
    string Phrase,
    TagntWordEntry Entry);

public sealed record GreekLexiconEntry(
    string EStrong,
    string DStrong,
    string UStrong,
    string Greek,
    string Transliteration,
    string Morph,
    string Gloss,
    string Meaning);

public sealed class BibleTranslationFile
{
    public List<BibleTranslationVerse> Verses { get; set; } = new();
}

public sealed class BibleTranslationVerse
{
    [JsonPropertyName("book_name")]
    public string BookName { get; set; } = string.Empty;

    [JsonPropertyName("chapter")]
    public int Chapter { get; set; }

    [JsonPropertyName("verse")]
    public int Verse { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public sealed record EndStudySessionApiRequest(
    string StudyName,
    string PassageReference,
    string ScriptureText,
    bool OverviewEnabled,
    string OverviewPrompt,
    bool RemindersEnabled,
    List<StudyReminderApiRequest> Reminders);

public sealed record StudyReminderApiRequest(
    string Key,
    string Title,
    string Instruction,
    string Delay);

public sealed record EndStudySessionApiResponse(
    string Overview,
    List<NotificationDispatchApiResult> Notifications);

public sealed record CancelNotificationsApiRequest(
    List<string> SequenceIds);

public sealed record CancelNotificationsApiResponse(
    List<NotificationCancelApiResult> Notifications);

public sealed record NotificationDispatchApiResult(
    string Title,
    string Delivery,
    string SequenceId,
    bool Sent,
    string? Error);

public sealed record NotificationCancelApiResult(
    string SequenceId,
    bool Canceled,
    string? Error);
