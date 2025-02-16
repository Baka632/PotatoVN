﻿using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Pickers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Contracts.Services;
using GalgameManager.Contracts.ViewModels;
using GalgameManager.Core.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Helpers;
using GalgameManager.Models;
using GalgameManager.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Security.Credentials.UI;
using Windows.Security.Credentials;
using GalgameManager.Views;

namespace GalgameManager.ViewModels;


public partial class SettingsViewModel : ObservableRecipient, INavigationAware
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly GalgameCollectionService _galgameCollectionService;
    private readonly INavigationService _navigationService;
    private readonly IUpdateService _updateService;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ICategoryService _categoryService;
    private readonly IBgmOAuthService _bgmOAuthService;
    private string _versionDescription;

    #region UI_STRINGS

    private static readonly ResourceLoader ResourceLoader = new();
    public readonly string UiThemeTitle = ResourceLoader.GetString("SettingsPage_ThemeTitle");
    public readonly string UiThemeDescription = ResourceLoader.GetString("SettingsPage_ThemeDescription");
    public readonly string UiRssTitle = ResourceLoader.GetString("SettingsPage_RssTitle");
    public readonly string UiRssDescription = ResourceLoader.GetString("SettingsPage_RssDescription");
    public readonly string UiRssBgmPlaceholder = ResourceLoader.GetString("SettingsPage_Rss_BgmPlaceholder");
    public readonly string UiDownloadTitle = ResourceLoader.GetString("SettingsPage_DownloadTitle");
    public readonly string UiDownloadDescription = ResourceLoader.GetString("SettingsPage_DownloadDescription");
    public readonly string UiCloudSyncTitle = ResourceLoader.GetString("SettingsPage_CloudSyncTitle");
    public readonly string UiCloudSyncDescription = ResourceLoader.GetString("SettingsPage_CloudSyncDescription");
    public readonly string UiCloudSyncRoot = ResourceLoader.GetString("SettingsPage_CloudSync_Root");
    public readonly string UiSelect = ResourceLoader.GetString("Select");
    public readonly string UiAbout = ResourceLoader.GetString("Settings_AboutDescription").Replace("\\n", "\n");
    public readonly string UiLibraryTitle = "SettingsPage_LibraryTitle".GetLocalized();
    public readonly string UiLibraryDescription = "SettingsPage_LibraryDescription".GetLocalized();
    public readonly string UiLibraryMetaBackup = "SettingsPage_Library_MetaBackup".GetLocalized();
    public readonly string UiLibraryMetaBackupDescription = "SettingsPage_Library_MetaBackupDescription".GetLocalized();
    public readonly string UiLibrarySearchSubPath = "SettingsPage_Library_SearchSubPath".GetLocalized();
    public readonly string UiLibrarySearchSubPathDescription = "SettingsPage_Library_SearchSubPathDescription".GetLocalized();
    public readonly string UiLibrarySearchSubPathDepth = "SettingsPage_Library_SearchSubPathDepth".GetLocalized();
    public readonly string UiLibrarySearchSubPathDepthDescription = "SettingsPage_Library_SearchSubPathDepthDescription".GetLocalized();
    public readonly string UiLibraryNameDescription = "SettingsPage_Library_NameDescription".GetLocalized();
    public readonly string UiLibrarySearchRegex = "SettingsPage_Library_SearchRegex".GetLocalized();
    public readonly string UiLibrarySearchRegexDescription = "SettingsPage_Library_SearchRegexDescription".GetLocalized();
    public readonly string UiLibrarySearchRegexIndex = "SettingsPage_Library_SearchRegexIndex".GetLocalized();
    public readonly string UiLibrarySearchRegexIndexDescription = "SettingsPage_Library_SearchRegexIndexDescription".GetLocalized();
    public readonly string UiLibrarySearchRegexRemoveBorder = "SettingsPage_Library_SearchRegexRemoveBorder".GetLocalized();
    public readonly string UiLibrarySearchRegexRemoveBorderDescription = "SettingsPage_Library_SearchRegexRemoveBorderDescription".GetLocalized();
    public readonly string UiLibrarySearchRegexTryItOut = "SettingsPage_Library_SearchRegexTryItOut".GetLocalized();
    public readonly string UiLibraryGameSearchRule = "SettingsPage_Library_GameSearchRule".GetLocalized();
    public readonly string UiLibraryGameSearchRuleDescription = "SettingsPage_Library_GameSearchRuleDescription".GetLocalized();
    public readonly string UiLibraryGameSearchRuleMustContain = "SettingsPage_Library_GameSearchRuleMustContain".GetLocalized();
    public readonly string UiLibraryGameSearchRuleShouldContain = "SettingsPage_Library_GameSearchRuleShouldContain".GetLocalized();

    #endregion

    public string VersionDescription
    {
        get => _versionDescription;
        set => SetProperty(ref _versionDescription, value);
    }
    
    public async void OnNavigatedTo(object parameter)
    {
        if (_shouldDisplayUpdateNotification)
        {
            await ShowUpdateNotification();
            await _updateService.UpdateSettingsBadgeAsync();
        }

        await LoadBgmAccountAsync(await _bgmOAuthService.GetBgmAccountWithCache());
    }

    public void OnNavigatedFrom() { }

    public SettingsViewModel(IThemeSelectorService themeSelectorService, ILocalSettingsService localSettingsService, 
        IDataCollectionService<Galgame> galgameService, IUpdateService updateService, INavigationService navigationService,
        ICategoryService categoryService, IBgmOAuthService bgmOAuthService)
    {
        _categoryService = categoryService;
        _themeSelectorService = themeSelectorService;
        _navigationService = navigationService;
        _updateService = updateService;
        _bgmOAuthService = bgmOAuthService;
        updateService.SettingBadgeEvent += result => _shouldDisplayUpdateNotification = result;
        updateService.UpdateSettingsBadgeAsync(); //只是为了触发事件，原地TP，先这么写吧
        _versionDescription = GetVersionDescription();
        _localSettingsService = localSettingsService;
        
        //THEME
        _elementTheme = themeSelectorService.Theme;
        _fixHorizontalPicture = _localSettingsService.ReadSettingAsync<bool>(KeyValues.FixHorizontalPicture).Result;
        //OAUTH
        _bgmOAuthService.OnAuthResultChange += BgmAuthResultNotify;
        //RSS
        RssType = _localSettingsService.ReadSettingAsync<RssType>(KeyValues.RssType).Result;
        //DOWNLOAD_BEHAVIOR
        _overrideLocalName = _localSettingsService.ReadSettingAsync<bool>(KeyValues.OverrideLocalName).Result;
        _overrideLocalNameWithCNByBangumi = _localSettingsService.ReadSettingAsync<bool>(KeyValues.OverrideLocalNameWithCNByBangumi).Result;
        _autoCategory = _localSettingsService.ReadSettingAsync<bool>(KeyValues.AutoCategory).Result;
        _downloadPlayStatusWhenPhrasing = _localSettingsService.ReadSettingAsync<bool>(KeyValues.SyncPlayStatusWhenPhrasing).Result;
        //LIBRARY
        _galgameCollectionService = ((GalgameCollectionService?)galgameService)!;
        _galgameCollectionService.MetaSavedEvent += SetSaveMetaPopUp;
        _metaBackup = _localSettingsService.ReadSettingAsync<bool>(KeyValues.SaveBackupMetadata).Result;
        _searchSubFolder = _localSettingsService.ReadSettingAsync<bool>(KeyValues.SearchChildFolder).Result;
        _searchSubFolderDepth = _localSettingsService.ReadSettingAsync<int>(KeyValues.SearchChildFolderDepth).Result;
        _ignoreFetchResult = _localSettingsService.ReadSettingAsync<bool>(KeyValues.IgnoreFetchResult).Result;
        _regex = _localSettingsService.ReadSettingAsync<string>(KeyValues.RegexPattern).Result ?? ".+";
        _regexIndex = _localSettingsService.ReadSettingAsync<int>(KeyValues.RegexIndex).Result;
        _regexRemoveBorder = _localSettingsService.ReadSettingAsync<bool>(KeyValues.RegexRemoveBorder).Result;
        _gameFolderMustContain = _localSettingsService.ReadSettingAsync<string>(KeyValues.GameFolderMustContain).Result ?? "";
        _gameFolderShouldContain = _localSettingsService.ReadSettingAsync<string>(KeyValues.GameFolderShouldContain).Result ?? "";
        //CLOUD
        RemoteFolder = _localSettingsService.ReadSettingAsync<string>(KeyValues.RemoteFolder).Result ?? "";
        //QUICK_START
        _startPage = _localSettingsService.ReadSettingAsync<PageEnum>(KeyValues.StartPage).Result;
        QuitStart = _localSettingsService.ReadSettingAsync<bool>(KeyValues.QuitStart).Result;
        _authenticationType = _localSettingsService.ReadSettingAsync<AuthenticationType>(KeyValues.AuthenticationType).Result;
        //UPLOAD
        UploadToAppCenter = _localSettingsService.ReadSettingAsync<bool>(KeyValues.UploadData).Result;

        //Check the availability of Windows Hello
        UserConsentVerifierAvailability verifierAvailability = UserConsentVerifier.CheckAvailabilityAsync().AsTask().Result;
        AuthenticationTypes = verifierAvailability != UserConsentVerifierAvailability.Available
            ? new[] { AuthenticationType.NoAuthentication, AuthenticationType.CustomPassword }
            : new[] { AuthenticationType.NoAuthentication, AuthenticationType.WindowsHello, AuthenticationType.CustomPassword };
        _localSettingsService.OnSettingChanged += OnSettingChange;
    }

    #region INFOBAR_CONTROL

    [ObservableProperty] private string _infoBarMsg = string.Empty;
    [ObservableProperty] private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
    [ObservableProperty] private bool _isInfoBarOpen;
    private int _displayIndex;

    /// <summary>
    /// 使用InfoBar显示消息
    /// </summary>
    /// <param name="severity">严重程度</param>
    /// <param name="msg">消息本体</param>
    /// <param name="time">显示时间(ms)</param>
    private async Task DisplayMsgAsync(InfoBarSeverity severity, string msg, int time = 3000)
    {
        var index = ++_displayIndex;
        InfoBarSeverity = severity;
        InfoBarMsg = msg;
        IsInfoBarOpen = true;
        await Task.Delay(time);
        if (index == _displayIndex)
            IsInfoBarOpen = false;
    }

    #endregion

    #region UPDATE

    private bool _shouldDisplayUpdateNotification;
    
    private async Task ShowUpdateNotification()
    {
        ContentDialog updateDialog = new()
        {
            XamlRoot = App.MainWindow.Content.XamlRoot,
            Title = "SettingsPage_UpdateNotification_Title".GetLocalized(),
            Content = "SettingsPage_UpdateNotification_Msg".GetLocalized(),
            PrimaryButtonText = "SettingsPage_SeeWhatsNew".GetLocalized(),
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary
        };
        updateDialog.PrimaryButtonClick += (_, _) =>
            _navigationService.NavigateTo(typeof(UpdateContentViewModel).FullName!);
        await _localSettingsService.SaveSettingAsync(KeyValues.LastNoticeUpdateVersion, RuntimeHelper.GetVersion());
        await updateDialog.ShowAsync();
    }
    
    #endregion

    #region THEME
    public readonly ElementTheme[] Themes = { ElementTheme.Default, ElementTheme.Light, ElementTheme.Dark };
    [ObservableProperty ]private ElementTheme _elementTheme;
    
    partial void OnElementThemeChanged(ElementTheme value)
    {
        _themeSelectorService.SetThemeAsync(value);
    }

    [ObservableProperty] private bool _fixHorizontalPicture;
    partial void OnFixHorizontalPictureChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.FixHorizontalPicture, value);

    #endregion

    #region OAUTH
    
    [ObservableProperty] private string _bgmStateString = string.Empty;
    [ObservableProperty] private string _bgmAvatar = string.Empty;
    [ObservableProperty] private string _bgmUserName = string.Empty;
    [ObservableProperty] private string _bgmButtonText = string.Empty;
    private BgmAccount _bgmAccount = new();
    
    [RelayCommand]
    private async Task ChangeBgmState()
    {
        if (_bgmAccount.OAuthed)
            await _bgmOAuthService.QuitLoginBgm();
        else
            await _bgmOAuthService.StartOAuth();
    }

    private async void OnSettingChange(string key, object value)
    {
        switch (key)
        {
            case KeyValues.BangumiOAuthState:
                await LoadBgmAccountAsync((value as BgmAccount)!); 
                break;
        }
    }

    private async Task LoadBgmAccountAsync(BgmAccount account)
    {
        _bgmAccount = account;
        BgmAvatar = account.Avatar;
        BgmUserName = account.Name;
        BgmStateString = await _bgmOAuthService.GetOAuthStateString();
        BgmButtonText = account.OAuthed ? "Logout".GetLocalized() : "Login".GetLocalized();
    }
    
    private async void BgmAuthResultNotify((OAuthResult, string) arg)
    {
        switch (arg.Item1)
        {
            case OAuthResult.Done:
            case OAuthResult.Failed:
                await DisplayMsgAsync(arg.Item1.ToInfoBarSeverity(), arg.Item2);
                break;
            case OAuthResult.FetchingAccount: 
            case OAuthResult.FetchingToken:
            default:
                await DisplayMsgAsync(arg.Item1.ToInfoBarSeverity(), arg.Item2);
                break;
        }
    }

    #endregion

    #region RSS

    [ObservableProperty] private RssType _rssType;
    // ReSharper disable once CollectionNeverQueried.Global
    public readonly RssType[] RssTypes = { RssType.Mixed , RssType.Bangumi, RssType.Vndb};
    
    partial void OnRssTypeChanged(RssType value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.RssType, value);
    }


    #endregion

    #region DOWNLOAD_BEHAVIOR

    [ObservableProperty] private bool _overrideLocalName;
    [ObservableProperty] private bool _overrideLocalNameWithCNByBangumi;
    [ObservableProperty] private bool _autoCategory;
    [ObservableProperty] private bool _downloadPlayStatusWhenPhrasing;

    partial void OnOverrideLocalNameChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.OverrideLocalName, value);
    
    partial void OnOverrideLocalNameWithCNByBangumiChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.OverrideLocalNameWithCNByBangumi, value);
    
    partial void OnAutoCategoryChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.AutoCategory, value);
    
    partial void OnDownloadPlayStatusWhenPhrasingChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.SyncPlayStatusWhenPhrasing, value);

    [RelayCommand]
    private async Task CategoryNow()
    {
        await _categoryService.UpdateAllGames();
    }

    #endregion

    #region LIBRARY

    [ObservableProperty] private bool _metaBackup;
    [ObservableProperty] private string _metaBackupProgress = "";
    [ObservableProperty] private bool _searchSubFolder;
    [ObservableProperty] private int _searchSubFolderDepth;
    [ObservableProperty] private bool _ignoreFetchResult;
    [ObservableProperty] private string _regex;
    [ObservableProperty] private int _regexIndex;
    [ObservableProperty] private bool _regexRemoveBorder;
    [ObservableProperty] private string _gameFolderMustContain;
    [ObservableProperty] private string _gameFolderShouldContain;
    [ObservableProperty] private string _regexTryItOut = "";

    partial void OnMetaBackupChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.SaveBackupMetadata, value);

    partial void OnSearchSubFolderChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.SearchChildFolder, value);

    partial void OnSearchSubFolderDepthChanged(int value) => _localSettingsService.SaveSettingAsync(KeyValues.SearchChildFolderDepth, value);

    partial void OnIgnoreFetchResultChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.IgnoreFetchResult, value);
    
    partial void OnRegexChanged(string value) => _localSettingsService.SaveSettingAsync(KeyValues.RegexPattern, value);

    partial void OnRegexIndexChanged(int value) => _localSettingsService.SaveSettingAsync(KeyValues.RegexIndex, value);

    partial void OnRegexRemoveBorderChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.RegexRemoveBorder, value);

    partial void OnGameFolderShouldContainChanged(string value) => _localSettingsService.SaveSettingAsync(KeyValues.GameFolderShouldContain, value);

    partial void OnGameFolderMustContainChanged(string value) => _localSettingsService.SaveSettingAsync(KeyValues.GameFolderMustContain, value);

    [RelayCommand]
    private void OnRegexTryItOut() => RegexTryItOut = NameRegex.GetName(_regexTryItOut, _regex, _regexRemoveBorder, _regexIndex);

    private void SetSaveMetaPopUp(Galgame galgame)
    {
        MetaBackupProgress = "SettingsPage_Library_MetaBackupProgress".GetLocalized() + galgame.Name.Value;
    }

    [RelayCommand]
    private async Task SaveMetaBackUp()
    {
        await _galgameCollectionService.SaveAllMetaAsync();
        MetaBackupProgress = "Done!";
    }

    #endregion

    #region CLOUD

    [ObservableProperty] private string? _remoteFolder;
    partial void OnRemoteFolderChanged(string? value)
    {
        _localSettingsService.SaveSettingAsync(KeyValues.RemoteFolder, value);
    }
    [RelayCommand]
    private async void SelectRemoteFolder()
    {
        FolderPicker openPicker = new();
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, App.MainWindow.GetWindowHandle());
        openPicker.SuggestedStartLocation = PickerLocationId.HomeGroup;
        openPicker.FileTypeFilter.Add("*");
        StorageFolder? folder = await openPicker.PickSingleFolderAsync();
        RemoteFolder = folder?.Path;
    }

    #endregion

    #region QUIT_START

    [ObservableProperty] private bool _quitStart;
    public readonly PageEnum[] StartPages = { PageEnum.Home , PageEnum.Category};
    [ObservableProperty] private PageEnum _startPage;
    public readonly AuthenticationType[] AuthenticationTypes;
    [ObservableProperty] private AuthenticationType _authenticationType;

    partial void OnQuitStartChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.QuitStart, value);

    partial void OnStartPageChanged(PageEnum value) => _localSettingsService.SaveSettingAsync(KeyValues.StartPage, value);

    async partial void OnAuthenticationTypeChanged(AuthenticationType value)
    {
        switch (value)
        {
            case AuthenticationType.NoAuthentication:
            case AuthenticationType.WindowsHello:
                break;
            case AuthenticationType.CustomPassword:
                var result = await TrySetCustomPassword();
                if (!result)
                {
                    AuthenticationType = AuthenticationType.NoAuthentication;
                    return;
                }
                break;
        }

        await _localSettingsService.SaveSettingAsync(KeyValues.AuthenticationType, value);
    }

    private async Task<bool> TrySetCustomPassword()
    {
        PasswordDialog passwordDialog = new()
        {
            Title = "SetYourPasswordLiteral".GetLocalized(),
            Message = "SaveYourPasswordCarefullyLiteral".GetLocalized(),
            PrimaryButtonText = "ConfirmLiteral".GetLocalized(),
            CloseButtonText = "Cancel".GetLocalized(),
            PasswordBoxPlaceholderText = "PasswordLiteral".GetLocalized(),
        };
        await passwordDialog.ShowAsync();

        var password = passwordDialog.Password;
        if (string.IsNullOrEmpty(password) is not true)
        {
            PasswordCredential credential = new(KeyValues.CustomPasswordSaverName, KeyValues.CustomPasswordDisplayName, password);
            new PasswordVault().Add(credential);
            return true;
        }
        else
        {
            return false;
        }
    }

    #endregion

    #region UPLOAD

    [ObservableProperty] private bool _uploadToAppCenter;
    
    partial void OnUploadToAppCenterChanged(bool value) => _localSettingsService.SaveSettingAsync(KeyValues.UploadData, value);

    #endregion

    #region ABOUT

    [RelayCommand]
    private async void Rate()
    {
        StoreContext context = StoreContext.GetDefault();
        WinRT.Interop.InitializeWithWindow.Initialize(context, App.MainWindow.GetWindowHandle());
        await context.RequestRateAndReviewAppAsync();
    }

    [RelayCommand]
    private void UpdateContent()
    {
        _navigationService.NavigateTo(typeof(UpdateContentViewModel).FullName!);
    }
    
    private static string GetVersionDescription()
    {
        return $"{"AppDisplayName".GetLocalized()} - {RuntimeHelper.GetVersion()}";
    }

    #endregion
}
