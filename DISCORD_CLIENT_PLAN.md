# Discord Client 구현 플랜

현재 `KakaoBotAT.Server`의 명령 처리 체계와 `KakaoBotAT.Commons` DTO를 그대로 활용하고, 신규 `Discord` 클라이언트 프로젝트를 추가해 메시지 수신/전송 어댑터만 분리한다. 핵심은 서버 코드 무수정 원칙(`/api/kakao/notify`, `/api/kakao/command` 재사용), ID 매핑 규칙 표준화, 그리고 테스트 프로젝트 부재 상태를 보완하는 검증 체계 수립이다.

## 0) 실행 체크리스트

- [x] 솔루션/프로젝트/코드 레벨 분석 완료
- [x] 서버 재사용 계약(엔드포인트, DTO, 응답 액션) 고정
- [x] `KakaoBotAT.DiscordClient` 프로젝트 스캐폴딩
- [x] 메시지 수신/매핑/API 호출/응답 전송 구현 (기본 골격)
- [x] 폴링 기반 예약/알림 수신 구현 (기본 골격)
- [ ] 단위/통합/수동 테스트 완료

## 1) 분석 범위 (프로젝트-폴더-코드)

### 1.1 솔루션/프로젝트

- `AT-Kakao-Bot.slnx`
- `KakaoBotAT.Server/KakaoBotAT.Server.csproj`
- `KakaoBotAT.Commons/KakaoBotAT.Commons.csproj`
- `KakaoBotAT.MobileClient/KakaoBotAT.MobileClient.csproj`

### 1.2 서버 재사용 핵심 코드

- `KakaoBotAT.Server/Controllers/KakaoController.cs`
- `KakaoBotAT.Server/Services/KakaoService.cs`
- `KakaoBotAT.Server/Commands/CommandHandlerFactory.cs`
- `KakaoBotAT.Server/Commands/ICommandHandler.cs`
- `KakaoBotAT.Server/Program.cs`

### 1.3 모바일 클라이언트 참조 패턴

- `KakaoBotAT.MobileClient/ViewModels/MainViewModel.cs`
- `KakaoBotAT.MobileClient/Platforms/Android/KakaoNotificationListener.cs`

### 1.4 분석 산출물

- [ ] DTO 필드 대응표
- [ ] API 호출 시퀀스 다이어그램
- [ ] 명령 처리 진입점/종료점 표
- [ ] Discord 매핑 정책(식별자/권한/채널 범위)

## 2) 서버 재사용 전략

## 2.1 API 재사용 원칙

1. 1차 구현에서 서버 엔드포인트는 변경하지 않는다.
2. Discord 클라이언트도 `/api/kakao/notify`, `/api/kakao/command`를 호출한다.
3. 서버 변경은 2차(필요 시)로 분리한다.

### 2.2 DTO 매핑 규칙 (Discord -> Commons)

- `roomId` <- Discord `ChannelId` (문자열)
- `roomName` <- `GuildName/ChannelName`
- `senderHash` <- Discord `UserId`
- `senderName` <- Discord 표시명
- `content` <- 원문 메시지

### 2.3 명령 처리/응답 매핑

1. Discord 메시지를 `ServerNotification`으로 변환
2. `POST /api/kakao/notify` 호출
3. 응답 `ServerResponse.Action` 처리
   - `send_text`: Discord 채널 메시지 전송
   - `read`: Discord에서 no-op(또는 내부 ack 로그)
   - `error`: 운영 로그 + 사용자 안내 메시지 정책 적용

### 2.4 폴링 재사용

- 주기적으로 `GET /api/kakao/command?availableRooms=...` 호출
- `availableRooms`는 전송 권한이 있는 Discord 채널 ID 목록으로 구성
- 예약 메시지/IMAX 알림 수신 시 채널에 전송

## 3) Discord 클라이언트 설계

### 3.1 신규 프로젝트

- `KakaoBotAT.DiscordClient/` (콘솔 또는 Worker)
- `KakaoBotAT.DiscordClient.Tests/` (xUnit)

### 3.2 폴더 구조

- `Adapters/` (Discord SDK 수신/전송)
- `Contracts/` (Commons 매핑 규칙)
- `Services/` (`ServerApiClient`, `PollingService`, `MessageRouter`)
- `Configuration/` (토큰, 서버 URL, allowlist)

### 3.3 핵심 컴포넌트

- `DiscordEventListener`: 디스코드 이벤트 수신
- `DtoMapper`: Discord 이벤트 -> `ServerNotification`
- `ServerApiClient`: 서버 API 호출
- `DiscordCommandExecutor`: `ServerResponse` 액션 실행
- `RoomAvailabilityProvider`: `availableRooms` 계산

### 3.4 데이터 흐름

1. Discord 메시지 수신
2. DTO 매핑
3. `POST /notify`
4. 즉시 응답 전송
5. 백그라운드 `GET /command` 폴링
6. 예약/알림 응답 전송

## 4) 구현 단계별 작업

### 4.1 단계 A: 분석/고정

- [ ] DTO 매핑표 확정
- [ ] 오류 처리/재시도 정책 확정
- [ ] 식별자 키 정책 확정 (`ChannelId` vs `GuildId:ChannelId`)
- [ ] `/api/kakao/notify`, `/api/kakao/command` 계약 고정

### 4.2 단계 B: 스캐폴딩

- [ ] `KakaoBotAT.DiscordClient` 프로젝트 생성
- [ ] `AT-Kakao-Bot.slnx`에 프로젝트 등록
- [ ] `KakaoBotAT.Commons` 참조 추가
- [ ] 기본 설정 파일 및 옵션 바인딩 추가

### 4.3 단계 C: 핵심 기능

- [ ] Discord 수신 어댑터 구현
- [ ] `DtoMapper` 구현
- [ ] `ServerApiClient` 구현 (`/api/kakao/notify`, `/api/kakao/command`)
- [ ] `DiscordCommandExecutor` 구현
- [ ] 폴링 백그라운드 서비스 구현

### 4.4 단계 D: 운영 기능

- [ ] 로그 태그 표준화
- [ ] 재시도/백오프 정책 적용
- [ ] 채널 allowlist/권한 검증 적용

### 4.5 단계 E: 안정화

- [ ] 예외 상황(권한 없음, 채널 삭제, 서버 타임아웃) 처리
- [ ] graceful shutdown/취소 토큰 처리
- [ ] 설정 검증(토큰, 서버 URL, 채널 목록)

## 5) 테스트 플랜

### 5.1 단위 테스트 (`KakaoBotAT.DiscordClient.Tests`)

- [ ] `DtoMapper` 필드 매핑 검증
- [ ] `ServerApiClient` 직렬화/역직렬화 검증
- [ ] `ServerResponse.Action` 분기 처리 검증

### 5.2 통합 테스트

- [ ] Discord 메시지 -> `/notify` -> Discord 응답 전송
- [ ] `/command` 폴링 -> 예약/알림 전송
- [ ] 서버 4xx/5xx/타임아웃 시 재시도/로그 검증

### 5.3 수동 시나리오

- [ ] 실제 채널에서 `댕`, `!도움말`, 관리자 명령 일부 점검
- [ ] 멀티 채널 동시 메시지에서 `roomId` 분리 점검
- [ ] 토큰/권한/서버주소 오설정 점검

## 6) 리스크와 의사결정 포인트

### 6.1 API 경로

- 옵션 A: 기존 `/api/kakao/*` 재사용 (빠름, 변경 최소)
- 옵션 B: `/api/discord/*` 분리 (명확하지만 서버 변경 증가)
- 결정: 옵션 A 확정

### 6.2 명령 수신 방식

- 옵션 A: 폴링 유지 (기존 즉시 활용)
- 옵션 B: 서버 푸시 확장 (효율적이나 구조 변경 필요)

### 6.3 room 식별자

- 현행 room 자료구조 유지(당분간 변경 없음)
- `roomId` 정책도 현재 정의를 유지하고, 구조 개편은 2차로 보류

## 7) 1차 권장안

- API 경로: A (`/api/kakao/*` 재사용)
- 명령 수신: A (폴링)
- room 관련: 현행 유지(자료구조/식별자 정책 변경 없음)

위 3개를 1차 기본값으로 고정하면 서버 수정 없이 Discord 클라이언트를 빠르게 가동할 수 있다.

