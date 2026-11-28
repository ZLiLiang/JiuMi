using JiuMi.Application.MyJiuMi;

namespace JiuMi.Application.Tests;

/// <summary>
/// Verifies the JiuMi native character pack manifest protocol (<c>schemaVersion: 1</c>): parsing,
/// anchor override precedence, optional section degradation and invalid manifest rejection.
/// </summary>
public sealed class MyJiuMiPetPackManifestLoaderTests
{
    private const string ValidMinimalManifestJson = """
        {
          "schemaVersion": 1,
          "id": "orange-cat",
          "name": "JiuMi Orange Cat",
          "canvas": { "width": 400, "height": 400 },
          "defaultAnchor": { "x": 200, "y": 360 },
          "clips": {
            "lying-idle": {
              "loop": true,
              "frames": [
                { "path": "lying-idle/00.png", "durationMs": 1500 },
                { "path": "lying-idle/01.png", "durationMs": 1500 }
              ]
            },
            "blink": {
              "loop": false,
              "anchor": { "x": 202, "y": 361 },
              "frames": [
                { "path": "blink/00.png", "durationMs": 60 },
                { "path": "blink/01.png", "durationMs": 90, "anchor": { "x": 205, "y": 362 } },
                { "path": "blink/02.png", "durationMs": 60 }
              ]
            },
            "react": {
              "loop": false,
              "frames": [
                { "path": "react/00.png", "durationMs": 120 }
              ]
            },
            "drag": {
              "loop": true,
              "frames": [
                { "path": "drag/00.png", "durationMs": 400 }
              ]
            },
            "sleep-in": {
              "loop": false,
              "frames": [
                { "path": "sleep-in/00.png", "durationMs": 150 }
              ]
            },
            "sleep": {
              "loop": true,
              "frames": [
                { "path": "sleep/00.png", "durationMs": 2000 }
              ]
            },
            "wake": {
              "loop": false,
              "frames": [
                { "path": "wake/00.png", "durationMs": 150 }
              ]
            },
            "stretch": {
              "loop": false,
              "frames": [
                { "path": "stretch/00.png", "durationMs": 120 }
              ]
            },
            "walk": {
              "loop": true,
              "frames": [
                { "path": "walk/00.png", "durationMs": 26 }
              ]
            },
            "jump": {
              "loop": false,
              "frames": [
                { "path": "jump/airborne.png", "durationMs": 504 },
                { "path": "jump/landing.png", "durationMs": 126 }
              ]
            }
          }
        }
        """;

    [Fact]
    public void Load_GivenValidManifest_ParsesMetadataClipsAndAnchorPrecedence()
    {
        MyJiuMiPetPackLoadResult result = MyJiuMiPetPackManifestLoader.Load(ValidMinimalManifestJson);

        Assert.True(result.IsSuccess, result.Error);
        MyJiuMiPetPackManifest manifest = Assert.IsType<MyJiuMiPetPackManifest>(result.Manifest);
        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("orange-cat", manifest.Id);
        Assert.Equal("JiuMi Orange Cat", manifest.Name);
        Assert.Equal(new MyJiuMiPetPackCanvas(400, 400), manifest.Canvas);
        Assert.Equal(new MyJiuMiPetPackAnchor(200, 360), manifest.DefaultAnchor);

        MyJiuMiPetPackClip lyingIdle = manifest.GetClip(MyJiuMiState.LyingIdle);
        Assert.True(lyingIdle.Loops);
        Assert.Equal(2, lyingIdle.Frames.Count);
        Assert.Equal("lying-idle/00.png", lyingIdle.Frames[0].Path);
        Assert.Equal(TimeSpan.FromMilliseconds(1500), lyingIdle.Frames[0].Duration);
        Assert.Equal(new MyJiuMiPetPackAnchor(200, 360), lyingIdle.Frames[0].Anchor);

        MyJiuMiPetPackClip blink = manifest.GetClip(MyJiuMiState.Blink);
        Assert.False(blink.Loops);
        Assert.Equal(new MyJiuMiPetPackAnchor(202, 361), blink.DefaultAnchor);
        Assert.Equal(new MyJiuMiPetPackAnchor(202, 361), blink.Frames[0].Anchor);
        Assert.Equal(new MyJiuMiPetPackAnchor(205, 362), blink.Frames[1].Anchor);
        Assert.Equal(new MyJiuMiPetPackAnchor(202, 361), blink.Frames[2].Anchor);

        Assert.True(manifest.TryGetJumpClip(out MyJiuMiPetPackClip? jump));
        Assert.NotNull(jump);
        Assert.Equal(2, jump.Frames.Count);
        Assert.Equal("jump/airborne.png", jump.Frames[0].Path);
        Assert.Equal("jump/landing.png", jump.Frames[1].Path);
    }

    [Fact]
    public void Load_GivenMissingOptionalInteractionsAndFiles_DegradesToEmptyDictionaries()
    {
        MyJiuMiPetPackLoadResult result = MyJiuMiPetPackManifestLoader.Load(ValidMinimalManifestJson);

        Assert.True(result.IsSuccess, result.Error);
        MyJiuMiPetPackManifest manifest = Assert.IsType<MyJiuMiPetPackManifest>(result.Manifest);
        Assert.Empty(manifest.Interactions);
        Assert.Empty(manifest.Files);
    }

    [Fact]
    public void Load_GivenOptionalInteractionsAndFiles_ParsesBoth()
    {
        string json = ValidMinimalManifestJson.TrimEnd()[..^1] + """
            ,
              "interactions": {
                "pet": {
                  "loop": false,
                  "frames": [
                    { "path": "petting/00.png", "durationMs": 200 }
                  ]
                }
              },
              "files": {
                "lying-idle/00.png": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
              }
            }
            """;

        MyJiuMiPetPackLoadResult result = MyJiuMiPetPackManifestLoader.Load(json);

        Assert.True(result.IsSuccess, result.Error);
        MyJiuMiPetPackManifest manifest = Assert.IsType<MyJiuMiPetPackManifest>(result.Manifest);
        Assert.True(manifest.Interactions.TryGetValue("pet", out MyJiuMiPetPackClip? pet));
        Assert.NotNull(pet);
        Assert.Single(pet.Frames);
        Assert.Equal("petting/00.png", pet.Frames[0].Path);
        Assert.Equal(new MyJiuMiPetPackAnchor(200, 360), pet.Frames[0].Anchor);
        Assert.Single(manifest.Files);
    }

    [Fact]
    public void Load_GivenSitWatchInteractions_EnablesSitWatchAndExposesAllFourSlots()
    {
        string json = ValidMinimalManifestJson.TrimEnd()[..^1] + """
            ,
              "interactions": {
                "sit-watch-left": {
                  "loop": true,
                  "frames": [
                    { "path": "sit-watch-left/00.png", "durationMs": 500 }
                  ]
                },
                "sit-watch-center": {
                  "loop": true,
                  "frames": [
                    { "path": "sit-watch-center/00.png", "durationMs": 500 }
                  ]
                },
                "sit-watch-right": {
                  "loop": true,
                  "frames": [
                    { "path": "sit-watch-right/00.png", "durationMs": 500 }
                  ]
                },
                "sit-to-lie": {
                  "loop": false,
                  "frames": [
                    { "path": "sit-to-lie/00.png", "durationMs": 125 },
                    { "path": "sit-to-lie/01.png", "durationMs": 125 },
                    { "path": "sit-to-lie/02.png", "durationMs": 125 },
                    { "path": "sit-to-lie/03.png", "durationMs": 125 }
                  ]
                }
              }
            }
            """;

        MyJiuMiPetPackLoadResult result = MyJiuMiPetPackManifestLoader.Load(json);

        Assert.True(result.IsSuccess, result.Error);
        MyJiuMiPetPackManifest manifest = Assert.IsType<MyJiuMiPetPackManifest>(result.Manifest);
        Assert.Equal(1, manifest.SchemaVersion);
        Assert.True(manifest.SupportsSitWatch);
        Assert.Equal(MyJiuMiSitWatchTier.ThreeWay, manifest.SitWatchTier);
        Assert.True(manifest.TryGetSitWatchClip(MyJiuMiSitWatchDirection.Left, out MyJiuMiPetPackClip? left));
        Assert.True(manifest.TryGetSitWatchClip(MyJiuMiSitWatchDirection.Center, out MyJiuMiPetPackClip? center));
        Assert.True(manifest.TryGetSitWatchClip(MyJiuMiSitWatchDirection.Right, out MyJiuMiPetPackClip? right));
        Assert.True(manifest.TryGetSitToLieClip(out MyJiuMiPetPackClip? sitToLie));
        Assert.Equal("sit-watch-left/00.png", left?.Frames[0].Path);
        Assert.Equal("sit-watch-center/00.png", center?.Frames[0].Path);
        Assert.Equal("sit-watch-right/00.png", right?.Frames[0].Path);
        Assert.Equal(TimeSpan.FromMilliseconds(500), sitToLie?.Duration);
    }

    [Fact]
    public void Load_GivenNineWaySitWatchInteractions_ReportsNineWayTierAndExposesAllNineSlots()
    {
        string json = BuildManifestWithSitWatchKeys(
            "sit-watch-center",
            "sit-watch-up",
            "sit-watch-up-right",
            "sit-watch-right",
            "sit-watch-down-right",
            "sit-watch-down",
            "sit-watch-down-left",
            "sit-watch-left",
            "sit-watch-up-left");

        MyJiuMiPetPackLoadResult result = MyJiuMiPetPackManifestLoader.Load(json);

        Assert.True(result.IsSuccess, result.Error);
        MyJiuMiPetPackManifest manifest = Assert.IsType<MyJiuMiPetPackManifest>(result.Manifest);
        Assert.True(manifest.SupportsSitWatch);
        Assert.Equal(MyJiuMiSitWatchTier.NineWay, manifest.SitWatchTier);

        foreach (MyJiuMiSitWatchDirection direction in MyJiuMiPetPackManifest.SitWatchDirections)
        {
            string expectedKey = MyJiuMiPetPackManifest.GetSitWatchInteractionKey(direction);
            Assert.True(manifest.TryGetDeclaredSitWatchClip(direction, out MyJiuMiPetPackClip? declared));
            Assert.Equal(expectedKey, declared?.Key);
            Assert.True(manifest.TryResolveSitWatchDirection(direction, out MyJiuMiSitWatchDirection resolved));
            Assert.Equal(direction, resolved);
            Assert.True(manifest.TryGetSitWatchClip(direction, out MyJiuMiPetPackClip? clip));
            Assert.Equal(expectedKey, clip?.Key);
        }
    }

    [Fact]
    public void Load_GivenFiveWaySitWatchInteractions_ReportsFiveWayTierAndDegradesDiagonals()
    {
        string json = BuildManifestWithSitWatchKeys(
            "sit-watch-center",
            "sit-watch-up",
            "sit-watch-right",
            "sit-watch-down",
            "sit-watch-left");

        MyJiuMiPetPackLoadResult result = MyJiuMiPetPackManifestLoader.Load(json);

        Assert.True(result.IsSuccess, result.Error);
        MyJiuMiPetPackManifest manifest = Assert.IsType<MyJiuMiPetPackManifest>(result.Manifest);
        Assert.True(manifest.SupportsSitWatch);
        Assert.Equal(MyJiuMiSitWatchTier.FiveWay, manifest.SitWatchTier);

        Assert.True(manifest.TryGetDeclaredSitWatchClip(MyJiuMiSitWatchDirection.Up, out _));
        Assert.True(manifest.TryGetDeclaredSitWatchClip(MyJiuMiSitWatchDirection.Down, out _));
        Assert.False(manifest.TryGetDeclaredSitWatchClip(MyJiuMiSitWatchDirection.UpLeft, out _));
        Assert.False(manifest.TryGetDeclaredSitWatchClip(MyJiuMiSitWatchDirection.DownRight, out _));

        Assert.True(manifest.TryResolveSitWatchDirection(MyJiuMiSitWatchDirection.Up, out MyJiuMiSitWatchDirection upResolved));
        Assert.Equal(MyJiuMiSitWatchDirection.Up, upResolved);
        Assert.True(manifest.TryResolveSitWatchDirection(MyJiuMiSitWatchDirection.Down, out MyJiuMiSitWatchDirection downResolved));
        Assert.Equal(MyJiuMiSitWatchDirection.Down, downResolved);
        Assert.True(manifest.TryResolveSitWatchDirection(MyJiuMiSitWatchDirection.UpLeft, out MyJiuMiSitWatchDirection upLeftResolved));
        Assert.Equal(MyJiuMiSitWatchDirection.Left, upLeftResolved);
        Assert.True(manifest.TryResolveSitWatchDirection(MyJiuMiSitWatchDirection.UpRight, out MyJiuMiSitWatchDirection upRightResolved));
        Assert.Equal(MyJiuMiSitWatchDirection.Right, upRightResolved);
        Assert.True(manifest.TryResolveSitWatchDirection(MyJiuMiSitWatchDirection.DownLeft, out MyJiuMiSitWatchDirection downLeftResolved));
        Assert.Equal(MyJiuMiSitWatchDirection.Left, downLeftResolved);
        Assert.True(manifest.TryResolveSitWatchDirection(MyJiuMiSitWatchDirection.DownRight, out MyJiuMiSitWatchDirection downRightResolved));
        Assert.Equal(MyJiuMiSitWatchDirection.Right, downRightResolved);
    }

    [Fact]
    public void Load_GivenPartialDiagonalOrVerticalSlots_DegradesThroughNineToFiveToThreeLadder()
    {
        // Missing DownLeft/DownRight degrades incomplete 9-way to 5-way.
        MyJiuMiPetPackManifest partialNine = Assert.IsType<MyJiuMiPetPackManifest>(
            MyJiuMiPetPackManifestLoader.Load(
                BuildManifestWithSitWatchKeys(
                    "sit-watch-center",
                    "sit-watch-up",
                    "sit-watch-right",
                    "sit-watch-down",
                    "sit-watch-left",
                    "sit-watch-up-left",
                    "sit-watch-up-right")).Manifest);
        Assert.Equal(MyJiuMiSitWatchTier.FiveWay, partialNine.SitWatchTier);

        // Missing Down degrades incomplete 5-way to 3-way.
        MyJiuMiPetPackManifest partialFive = Assert.IsType<MyJiuMiPetPackManifest>(
            MyJiuMiPetPackManifestLoader.Load(
                BuildManifestWithSitWatchKeys(
                    "sit-watch-center",
                    "sit-watch-up",
                    "sit-watch-right",
                    "sit-watch-left")).Manifest);
        Assert.Equal(MyJiuMiSitWatchTier.ThreeWay, partialFive.SitWatchTier);
        Assert.True(partialFive.TryResolveSitWatchDirection(MyJiuMiSitWatchDirection.Up, out MyJiuMiSitWatchDirection resolvedUp));
        Assert.Equal(MyJiuMiSitWatchDirection.Center, resolvedUp);
        Assert.True(partialFive.TryResolveSitWatchDirection(MyJiuMiSitWatchDirection.Down, out MyJiuMiSitWatchDirection resolvedDown));
        Assert.Equal(MyJiuMiSitWatchDirection.Center, resolvedDown);
        Assert.True(partialFive.TryResolveSitWatchDirection(MyJiuMiSitWatchDirection.UpLeft, out MyJiuMiSitWatchDirection resolvedUpLeft));
        Assert.Equal(MyJiuMiSitWatchDirection.Left, resolvedUpLeft);
        Assert.True(partialFive.TryResolveSitWatchDirection(MyJiuMiSitWatchDirection.DownRight, out MyJiuMiSitWatchDirection resolvedDownRight));
        Assert.Equal(MyJiuMiSitWatchDirection.Right, resolvedDownRight);
    }

    [Fact]
    public void Load_GivenPartialSitWatchInteractions_DegradesSitWatchSupportToFalse()
    {
        string json = ValidMinimalManifestJson.TrimEnd()[..^1] + """
            ,
              "interactions": {
                "sit-watch-left": {
                  "loop": true,
                  "frames": [
                    { "path": "sit-watch-left/00.png", "durationMs": 500 }
                  ]
                },
                "sit-watch-center": {
                  "loop": true,
                  "frames": [
                    { "path": "sit-watch-center/00.png", "durationMs": 500 }
                  ]
                }
              }
            }
            """;

        MyJiuMiPetPackLoadResult result = MyJiuMiPetPackManifestLoader.Load(json);

        Assert.True(result.IsSuccess, result.Error);
        MyJiuMiPetPackManifest manifest = Assert.IsType<MyJiuMiPetPackManifest>(result.Manifest);
        Assert.False(manifest.SupportsSitWatch);
        Assert.Equal(MyJiuMiSitWatchTier.None, manifest.SitWatchTier);
        Assert.False(manifest.TryResolveSitWatchDirection(MyJiuMiSitWatchDirection.Center, out _));
        Assert.False(manifest.TryGetSitWatchClip(MyJiuMiSitWatchDirection.Center, out _));
        Assert.False(manifest.TryGetSitToLieClip(out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not valid json")]
    [InlineData("{ \"schemaVersion\": 2 }")]
    [InlineData("{ \"schemaVersion\": 1, \"id\": \"\", \"name\": \"Cat\" }")]
    public void Load_GivenInvalidOrUnsupportedManifest_ReturnsFailureWithoutThrowing(string json)
    {
        MyJiuMiPetPackLoadResult result = MyJiuMiPetPackManifestLoader.Load(json);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Manifest);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void Load_GivenPathTraversalFramePath_ReturnsFailure()
    {
        string json = ValidMinimalManifestJson.Replace("lying-idle/00.png", "../secret.png", StringComparison.Ordinal);

        MyJiuMiPetPackLoadResult result = MyJiuMiPetPackManifestLoader.Load(json);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Manifest);
    }

    [Fact]
    public void Load_GivenNonPositiveFrameDuration_ReturnsFailure()
    {
        string json = ValidMinimalManifestJson.Replace("\"durationMs\": 1500", "\"durationMs\": 0", StringComparison.Ordinal);

        MyJiuMiPetPackLoadResult result = MyJiuMiPetPackManifestLoader.Load(json);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Manifest);
    }

    [Fact]
    public void Load_GivenOutOfBoundsAnchor_ReturnsFailure()
    {
        string json = ValidMinimalManifestJson.Replace("\"y\": 360", "\"y\": 450", StringComparison.Ordinal);

        MyJiuMiPetPackLoadResult result = MyJiuMiPetPackManifestLoader.Load(json);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Manifest);
    }

    [Fact]
    public void Load_GivenMissingRequiredCoreClip_ReturnsFailure()
    {
        string json = ValidMinimalManifestJson.Replace("\"walk\":", "\"other\":", StringComparison.Ordinal);

        MyJiuMiPetPackLoadResult result = MyJiuMiPetPackManifestLoader.Load(json);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Manifest);
    }

    [Fact]
    public void Load_GivenBuiltInOrangeCatManifest_MatchesCatalogAndReferencesExistingFiles()
    {
        string packDirectory = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "JiuMi", "Assets", "MyJiuMi"));
        string manifestPath = Path.Combine(packDirectory, "manifest.json");
        Assert.True(File.Exists(manifestPath), $"Built-in manifest not found at '{manifestPath}'.");

        using FileStream stream = File.OpenRead(manifestPath);
        MyJiuMiPetPackLoadResult result = MyJiuMiPetPackManifestLoader.Load(stream);

        Assert.True(result.IsSuccess, result.Error);
        MyJiuMiPetPackManifest manifest = Assert.IsType<MyJiuMiPetPackManifest>(result.Manifest);
        Assert.Equal(new MyJiuMiPetPackCanvas(400, 400), manifest.Canvas);
        Assert.Equal(new MyJiuMiPetPackAnchor(200, 360), manifest.DefaultAnchor);
        Assert.True(manifest.SupportsSitWatch);

        foreach (MyJiuMiState state in MyJiuMiPetPackManifest.CoreStates)
        {
            MyJiuMiAnimationClip legacy = MyJiuMiAnimationCatalog.GetClip(state);
            MyJiuMiPetPackClip clip = manifest.GetClip(state);
            Assert.Equal(legacy.Loops, clip.Loops);
            Assert.Equal(legacy.FrameDurations.Count, clip.Frames.Count);
            for (int index = 0; index < legacy.FrameDurations.Count; index++)
            {
                Assert.Equal(legacy.FrameDurations[index], clip.Frames[index].Duration);
                string frameFile = Path.Combine(packDirectory, clip.Frames[index].Path.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(frameFile), $"Manifest frame '{clip.Frames[index].Path}' does not exist.");
            }
        }

        foreach (MyJiuMiSitWatchDirection direction in Enum.GetValues<MyJiuMiSitWatchDirection>())
        {
            Assert.True(manifest.TryGetSitWatchClip(direction, out MyJiuMiPetPackClip? sitClip));
            Assert.NotNull(sitClip);
            foreach (MyJiuMiPetPackFrame frame in sitClip.Frames)
            {
                Assert.Equal(new MyJiuMiPetPackAnchor(200, 360), frame.Anchor);
                string frameFile = Path.Combine(packDirectory, frame.Path.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(frameFile), $"Sit-watch frame '{frame.Path}' does not exist.");
            }
        }

        Assert.True(manifest.TryGetJumpClip(out MyJiuMiPetPackClip? jumpClip));
        Assert.NotNull(jumpClip);
        Assert.Equal(2, jumpClip.Frames.Count);
        foreach (MyJiuMiPetPackFrame frame in jumpClip.Frames)
        {
            string frameFile = Path.Combine(packDirectory, frame.Path.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(frameFile), $"Jump frame '{frame.Path}' does not exist.");
        }
    }

    [Fact]
    public void Update_GivenCustomManifest_UsesManifestFrameDurations()
    {
        string json = ValidMinimalManifestJson.Replace("\"durationMs\": 1500", "\"durationMs\": 250", StringComparison.Ordinal);
        MyJiuMiPetPackManifest manifest = Assert.IsType<MyJiuMiPetPackManifest>(
            MyJiuMiPetPackManifestLoader.Load(json).Manifest);
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero));
        var machine = new MyJiuMiStateMachine(clock, new FixedRandom(0.9), manifest);

        machine.Activate(stageWidth: 800, positionX: 0.5);
        Assert.Equal(0, machine.FrameIndex);

        clock.Advance(TimeSpan.FromMilliseconds(260));
        machine.Update();
        Assert.Equal(1, machine.FrameIndex);
    }

    private static string BuildManifestWithSitWatchKeys(params string[] keys)
    {
        string entries = string.Join(
            ",\n",
            keys.Select(key => $$"""
                    "{{key}}": {
                      "loop": true,
                      "frames": [
                        { "path": "{{key}}/00.png", "durationMs": 500 }
                      ]
                    }
                """));
        return ValidMinimalManifestJson.TrimEnd()[..^1] + ",\n  \"interactions\": {\n" + entries + "\n  }\n}";
    }
}
