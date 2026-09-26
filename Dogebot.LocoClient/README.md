# Dogebot.LocoClient

`@arooong/kakao-cli`의 API 모드 서버와 `Dogebot.Server`를 연결하는 브리지 클라이언트입니다.
카카오톡 알림을 사용하는 `Dogebot.MobileClient`와 달리, LOCO 세션으로 수신한 메시지를 `Dogebot.Commons` DTO로 변환해 `/api/kakao/notify`를 호출하고 `/api/kakao/command`를 폴링합니다.

## 구성

- `Configuration/LocoClientOptions.cs`: 설정 모델
- `Configuration/LocoClientOptionsEnvironmentLoader.cs`: 환경 변수 로딩
- `Services/LocoCliApiClient.cs`: kakao-cli API 호출과 방별 WebSocket 수신(자동 재연결)
- `Services/DogebotServerApiClient.cs`: Dogebot.Server API 호출(API 키 인증)
- `Services/LocoBridgeService.cs`: 방 구독 관리, 수신 메시지 전달, 응답 실행, 명령 폴링

## 사전 준비

kakao-cli API 모드 서버를 먼저 실행합니다. 최초 실행 시 터미널에 표시된 QR 코드를 카카오톡 앱으로 스캔해야 합니다.

```bash
npm i -g @arooong/kakao-cli
kakao-cli --api-mode 8880
```

API 서버는 기본적으로 `127.0.0.1`에만 바인딩되고 인증이 없으므로 외부 네트워크에 노출하지 마십시오.

## 설정

`appsettings.json`을 사용하지 않고 환경 변수만 사용합니다.

| 환경 변수 | 설명 | 기본값 |
| --- | --- | --- |
| `LOCO_CLI_BASE_URL` | kakao-cli API 주소 | `http://127.0.0.1:8880` |
| `LOCO_SERVER_BASE_URL` | Dogebot.Server의 `/api/kakao` 주소 | `https://your-server-url.com/api/kakao` |
| `DOGEBOT_API_KEY` | 서버 공유 API 키 | 없음(필수) |
| `LOCO_COMMAND_POLL_INTERVAL_SECONDS` | `/command` 폴링 주기(초) | `5` |
| `LOCO_ROOM_REFRESH_INTERVAL_SECONDS` | 채팅방 목록 재조회 주기(초) | `30` |
| `LOCO_WEBSOCKET_RECONNECT_DELAY_SECONDS` | WebSocket 재연결 대기(초) | `5` |

## 실행

```powershell
$env:LOCO_SERVER_BASE_URL = "https://your-server-url.com/api/kakao"
$env:DOGEBOT_API_KEY = "your-api-key"
dotnet run --project .\Dogebot.LocoClient\Dogebot.LocoClient.csproj
```

## 주의사항

- kakao-cli 0.1.7 API에는 읽음 처리 엔드포인트가 없습니다. 다만 현재 `Dogebot.Server` 코드는 `read` 액션을 발행하지 않으므로 동작에는 영향이 없습니다.
- `Dogebot.MobileClient`와 동시에 실행하면 `Dogebot.Server`가 같은 메시지를 중복 제거하지 않으므로, 두 클라이언트가 함께 인식한 메시지에는 답장이 두 번 나갈 수 있습니다.
- kakao-cli는 비공식 LOCO 프로토콜을 사용하므로 카카오 서비스 약관 위반이며, 사용 시 계정이 영구 정지될 수 있습니다.
