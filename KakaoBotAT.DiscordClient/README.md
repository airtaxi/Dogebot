# KakaoBotAT.DiscordClient

기존 서버(`KakaoBotAT.Server`)를 수정하지 않고, Discord 이벤트를 `KakaoBotAT.Commons` DTO로 변환해
`/api/kakao/notify`, `/api/kakao/command`를 호출하는 브리지 클라이언트입니다.

## 구성

- `Adapters/DiscordNetGatewayClient.cs`: Discord 게이트웨이 수신/전송
- `Contracts/DiscordMessageMapper.cs`: Discord -> `ServerNotification` 매핑
- `Services/ServerApiClient.cs`: 서버 API 호출
- `Services/DiscordNotificationService.cs`: 수신 메시지 즉시 처리
- `Services/PollingService.cs`: 예약 명령 폴링 처리

## 설정

`appsettings.json`의 `Discord` 섹션을 설정합니다.

- `Token`: Discord Bot Token
- `ServerBaseUrl`: 예) `https://your-server-url.com/api/kakao`
- `PollIntervalSeconds`: 폴링 주기(초)
- `AllowedChannelIds`: 허용 채널 ID 목록 (비우면 수신된 채널 자동 추적)

## 실행

```powershell
dotnet run --project .\KakaoBotAT.DiscordClient\KakaoBotAT.DiscordClient.csproj
```

## 테스트

```powershell
dotnet test .\KakaoBotAT.DiscordClient.Tests\KakaoBotAT.DiscordClient.Tests.csproj
```

