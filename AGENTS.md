# Copilot Instructions

## General

- 답변은 한국어로 작성. 사용자는 대한민국 사람임.
- Git commit/push는 사용자가 명시적으로 요청하지 않는 한 절대 수행하지 않는다.
- Git commit은 영어로 작성한다.
- 빌드는 사용자가 요청하였을 때에만 진행한다.
- 별도의 안내가 없으면 참조한 파일의 주석 언어를 그대로 유지한다 (영어 주석 → 영어, 한국어 주석 → 한국어).

## Build Commands

```bash
# 전체 솔루션 빌드
dotnet build

# 서버만 빌드
dotnet build Dogebot.Server

# 서버 실행
dotnet run --project Dogebot.Server
```

테스트 프로젝트와 lint 도구는 없다.

## Architecture

카카오톡 봇 시스템으로, 3개 프로젝트로 구성된 분산 아키텍처:

- **Dogebot.MobileClient** — .NET MAUI Android 앱. Android `NotificationListenerService`로 카카오톡 알림을 가로채 서버로 전송하고, 폴링으로 명령을 수신하여 답장/읽음 처리를 수행한다.
- **Dogebot.Server** — ASP.NET Core Web API. 메시지를 받아 커맨드 핸들러로 처리하고, MongoDB에 통계를 저장한다.
- **Dogebot.Commons** — 클라이언트-서버 간 공유 DTO (`KakaoMessageData`, `ServerNotification`, `ServerResponse`).

통신 흐름:
1. 클라이언트 → `POST /api/kakao/notify` → 서버가 커맨드 핸들러 실행 → 즉시 응답 반환
2. 클라이언트 → `GET /api/kakao/command?availableRooms=...` → 서버가 대기 중인 예약 메시지/IMAX 알림 반환

서버 시작 시 흐름 (`Dogebot.Server/Program.cs`):
- `MigrationService.RunMigrationsAsync()` 실행
- `MessageCleanupService`로 블랙리스트 메시지 정리
- `IAdminService`/`IRequestLimitService`로 만료 승인 코드 정리

## Technology Stack

- .NET 10 / C# 14, Nullable reference types 전역 활성화
- MongoDB (MongoDB.Driver) — 통계, 관리자, 예약 메시지 등 저장
- Selenium + HtmlAgilityPack — 일부 커맨드에서 웹 스크래핑
- CommunityToolkit.Mvvm — MAUI 클라이언트 MVVM 패턴
- OpenWeatherMap API — 날씨 커맨드 (`WEATHER_API_KEY` 또는 `Weather:ApiKey`)
- 외부 데이터 소스 — IMAX API (`https://imax.kagamine-rin.com`), 아카 핫딜 (`https://arca.live/b/hotdeal`)

## Naming Conventions

| 대상 | 규칙 | 예시 |
|------|------|------|
| Private instance fields | `_camelCase` | `_wakeLock`, `_random` |
| Private static fields | `_camelCase` | `_cacheLock`, `_driverLock` |
| Properties, methods, classes, enums | `PascalCase` | `HandleAsync`, `SenderHash` |
| Async 메서드 | `Async` 접미사 | `HandleNotificationAsync` |
| 인터페이스 | `I` 접두사 | `ICommandHandler` |
| 커맨드 핸들러 | `{Name}CommandHandler` | `DiceCommandHandler` |
| 이벤트 리스너 | `On{컨트롤이름}{이벤트명}` (Click→Clicked) | `OnAddResourceButtonClicked` |

## C# Code Style

- Single-line `if`/`for`/`foreach`/`while`은 중괄호 생략을 권장하되, 기존 코드 스타일을 우선한다.
- ~100자 초과 시 다음 줄로 내리되 중괄호는 여전히 생략.
- Single-line 메서드는 expression-bodied syntax (`=>`) 사용.
- Single-line `try`/`catch`/`finally`:
  ```csharp
  try { /* code */ }
  catch (Exception exception) { /* handle */ }
  ```
- Primary constructor를 적극 사용.
- Collection expression (`[item1, item2]`, `[]`) 적극 사용.
- 새 코드에서는 불필요한 약어를 지양하되, 기존 코드의 통용 약어(`ex`, `Kst` 등)와 일관성을 유지한다.
- 최신 C# 언어 기능 적극 활용.

## XAML Style (MAUI)

- 컨테이너 내 요소 간격은 `Spacing`/`RowSpacing`/`ColumnSpacing` 우선 사용.
- `Margin`은 개별 요소의 특별한 위치 조정에만 사용.

## Command Handler Pattern

새 봇 커맨드를 추가할 때:

1. `Dogebot.Server\Commands\` 에 `ICommandHandler` 구현 클래스 생성.
2. `Program.cs`에 `builder.Services.AddSingleton<ICommandHandler, YourHandler>();` 등록.
3. `HelpCommandHandler.cs`에 명령어 설명 추가 (`!도움`, `!도움말`, `!help` 노출 목록).

핸들러 구현 규칙:
- Primary constructor로 의존성 주입 (`ILogger<T>`, 서비스 등).
- `Command` 프로퍼티에 트리거 문자열 정의 (예: `"!주사위"`).
- `CanHandle()`에서 `content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase)` 또는 `Equals()` 사용.
- `HandleAsync()`는 항상 `ServerResponse`를 반환 (`Action = "send_text"`, `RoomId`, `Message`).
- try-catch로 감싸고, catch에서 `logger.LogError(ex, "[TAG] ...")` 후 사용자 친화적 에러 메시지 반환.
- 로깅 시 `[HANDLER_NAME]` 태그를 첫 인자로 사용 (예: `[DICE]`, `[ADMIN_ADD]`).

```csharp
public class ExampleCommandHandler(ILogger<ExampleCommandHandler> logger) : ICommandHandler
{
    public string Command => "!예시";

    public bool CanHandle(string content) =>
        content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "응답 메시지"
            });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[EXAMPLE] Error processing command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "오류가 발생했습니다."
            });
        }
    }
}
```

## Dependency Injection

- 일반 서비스와 커맨드 핸들러는 `AddSingleton`으로 등록.
- 주기 실행 작업은 `AddHostedService`로 등록 (예: `ApprovalCodeCleanupService`, `ImaxNotificationCheckService`).
- `CommandHandlerFactory`가 `IEnumerable<ICommandHandler>`를 주입받아 핸들러를 검색.
- 핸들러 등록 순서가 우선순위를 결정한다 (`FindHandler`는 첫 번째 매칭 반환).

## MongoDB Models

- MongoDB 모델은 `[BsonId]`, `[BsonElement("camelCase")]` 어트리뷰트 사용.
- 문자열 프로퍼티는 `= string.Empty`로 초기화.
- 시간 값은 Unix timestamp (`long`, `DateTimeOffset.UtcNow.ToUnixTimeSeconds()`).
- 컬렉션은 collection expression으로 초기화 (`= []`).

## DTO / JSON

- `Dogebot.Commons`의 DTO에 `[JsonPropertyName("camelCase")]` 사용.
- `ServerResponse.Action` 값: `"send_text"`, `"read"`, `"error"`.

## MongoDB Configuration

서버는 환경변수를 우선 사용하고, 없으면 `appsettings.json`을 참조한다:
- `DB_HOST`, `DB_PORT`, `DB_ID`, `DB_PASSWORD` (환경변수)
- `MongoDB:Host`, `MongoDB:Port`, `MongoDB:UserId`, `MongoDB:Password`, `MongoDB:Database` (appsettings)

## MongoDB Service Pattern

새 서비스 추가 시:

1. `Models/` 에 BSON 모델 생성 (`[BsonId]`, `[BsonElement("camelCase")]`).
2. `Services/` 에 인터페이스 + 구현 클래스 생성.
3. `Program.cs`에 `builder.Services.AddSingleton<IYourService, YourService>();` 등록.

서비스 구현 규칙:
- 생성자에서 `IMongoDbService`를 주입받아 `GetCollection<T>("collectionName")` 호출.
- `CreateIndexes()` 메서드를 별도로 분리하고, 생성자에서 호출.
- 인덱스 생성에 constructor body가 필요하므로 primary constructor 대신 일반 생성자 사용.

```csharp
public class ExampleService : IExampleService
{
    private readonly IMongoCollection<ExampleModel> _examples;

    public ExampleService(IMongoDbService mongoDbService)
    {
        _examples = mongoDbService.Database.GetCollection<ExampleModel>("examples");
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<ExampleModel>.IndexKeys
            .Ascending(x => x.SomeField)
            .Ascending(x => x.AnotherField);
        var indexModel = new CreateIndexModel<ExampleModel>(indexKeys, new CreateIndexOptions { Unique = true });
        _examples.Indexes.CreateOne(indexModel);
    }
}
```

## MongoDB Migration Pattern

스키마 변경이나 데이터 마이그레이션이 필요할 때 `MigrationService`를 사용한다.

1. `MigrationService.RunMigrationsAsync()`에 새 마이그레이션 호출 추가:
   ```csharp
   await ApplyMigrationAsync(version, "MigrationName", MigrationMethodAsync);
   ```
2. 마이그레이션 메서드를 private async로 구현.
3. `ApplyMigrationAsync`가 `migrations` 컬렉션에서 버전 중복을 자동 체크하므로 수동 확인 불필요.
4. 마이그레이션은 **버전 번호 순서대로** 등록해야 한다.

