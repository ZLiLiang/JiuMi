using JiuMi.Application.Errors;

namespace JiuMi.Application.Tests;

public sealed class ErrorNoticeBoardTests
{
    [Fact]
    public void Offer_GivenUnregisteredCode_DoesNotPresentANotice()
    {
        var board = new ErrorNoticeBoard();

        bool offered = board.Offer(CreateRecord("Unknown.Code", ErrorLevel.Error));

        Assert.False(offered);
        Assert.False(board.ShowNext());
        Assert.Null(board.Current);
    }

    [Fact]
    public void ShowNext_GivenErrorAndWarning_ShowsErrorFirst()
    {
        var board = new ErrorNoticeBoard();
        board.Offer(CreateRecord(ErrorCodes.SettingsCorrupted, ErrorLevel.Warning));
        board.Offer(CreateRecord(ErrorCodes.SettingsSaveFailed, ErrorLevel.Error));

        Assert.True(board.ShowNext());
        Assert.Equal(ErrorCodes.SettingsSaveFailed, board.Current?.Code);

        Assert.True(board.DismissCurrent());
        Assert.True(board.ShowNext());
        Assert.Equal(ErrorCodes.SettingsCorrupted, board.Current?.Code);
    }

    [Fact]
    public void Offer_GivenSameCodeTwice_boardsOneNotice()
    {
        var board = new ErrorNoticeBoard();

        Assert.True(board.Offer(CreateRecord(ErrorCodes.SettingsSaveFailed, ErrorLevel.Error)));
        Assert.False(board.Offer(CreateRecord(ErrorCodes.SettingsSaveFailed, ErrorLevel.Error)));
        Assert.True(board.ShowNext());
        Assert.False(board.ShowNext());
        Assert.Equal(ErrorCodes.SettingsSaveFailed, board.Current?.Code);
    }

    [Fact]
    public void Offer_GivenRegisteredCode_ExposesTheConfiguredAction()
    {
        var board = new ErrorNoticeBoard();
        board.Offer(CreateRecord(ErrorCodes.SettingsSaveFailed, ErrorLevel.Error));

        board.ShowNext();

        Assert.Equal(ErrorNoticeAction.Retry, board.Current?.Action);
        Assert.False(board.Current?.IsPersistent);
    }

    [Fact]
    public void Offer_GivenPersistentCurrentAndNewError_DefersPersistentUntilNewErrorCloses()
    {
        var board = new ErrorNoticeBoard();
        board.Offer(CreateRecord(ErrorCodes.ConfigMemoryOnly, ErrorLevel.Error));
        board.ShowNext();

        Assert.True(board.Offer(CreateRecord(ErrorCodes.SettingsSaveFailed, ErrorLevel.Error)));
        Assert.Null(board.Current);
        Assert.True(board.ShowNext());
        Assert.Equal(ErrorCodes.SettingsSaveFailed, board.Current?.Code);

        Assert.True(board.DismissCurrent());
        Assert.True(board.ShowNext());
        Assert.Equal(ErrorCodes.ConfigMemoryOnly, board.Current?.Code);
        Assert.False(board.DismissCurrent());
    }

    [Fact]
    public void Offer_GivenPersistentCurrentAndNewWarning_DefersPersistentUntilWarningCloses()
    {
        var board = new ErrorNoticeBoard();
        board.Offer(CreateRecord(ErrorCodes.ConfigMemoryOnly, ErrorLevel.Error));
        board.ShowNext();

        Assert.True(board.Offer(CreateRecord(ErrorCodes.SettingsCorrupted, ErrorLevel.Warning)));
        Assert.True(board.ShowNext());
        Assert.Equal(ErrorCodes.SettingsCorrupted, board.Current?.Code);

        Assert.True(board.DismissCurrent());
        Assert.True(board.ShowNext());
        Assert.Equal(ErrorCodes.ConfigMemoryOnly, board.Current?.Code);
    }

    [Fact]
    public void Offer_GivenRecordLevelMismatch_UsesTheRegisteredLevel()
    {
        var board = new ErrorNoticeBoard();
        board.Offer(CreateRecord(ErrorCodes.SettingsCorrupted, ErrorLevel.Error));

        board.ShowNext();

        Assert.Equal(ErrorLevel.Warning, board.Current?.Level);
    }

    [Fact]
    public void DismissCurrent_GivenPersistentNotice_KeepsItVisible()
    {
        var board = new ErrorNoticeBoard();
        board.Offer(CreateRecord(ErrorCodes.ConfigMemoryOnly, ErrorLevel.Error));
        board.ShowNext();

        Assert.True(board.Current?.IsPersistent);
        Assert.False(board.DismissCurrent());
        Assert.Equal(ErrorCodes.ConfigMemoryOnly, board.Current?.Code);
    }

    private static ErrorRecord CreateRecord(string code, ErrorLevel level) =>
        new()
        {
            Timestamp = DateTimeOffset.UnixEpoch,
            Level = level,
            Code = code,
            Operation = "Settings.Load",
        };
}
