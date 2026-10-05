# ISMLTS student API (for the Android app)

The web app exposes a JSON API under `/api/v1` for the student Android app. Students log in with the same college
email and password they use on the website. Lecturers and admins can't use the API; they use the website.

- **Base URL, live site:** `https://<your-app>.azurewebsites.net/api/v1/`
- **Base URL, Android emulator against a PC running the web app:** `http://10.0.2.2:5269/api/v1/`
- **Try it in a browser:** run the web app in Development and open `http://localhost:5269/swagger`.

## Quick start

1. Run the web app (Visual Studio, `http` profile, or `dotnet run --project "ISMLTS(WebApp)" --launch-profile http`).
   In Development it fills an empty database with demo data; every demo password is `12345678`.
2. Log in:

   ```http
   POST /api/v1/auth/login
   Content-Type: application/json

   { "login": "st10000001@rcconnect.edu.za", "password": "12345678" }
   ```

   ```json
   {
     "accessToken": "eyJhbGciOiJIUzI1NiIs...",
     "accessTokenExpiresAt": "2026-10-01T10:00:00+00:00",
     "refreshToken": "q3V0b2tlbi1leGFtcGxl...",
     "refreshTokenExpiresAt": "2026-10-31T09:00:00+00:00",
     "student": { "studentId": 1, "name": "Thandi Mokoena", "email": "st10000001@rcconnect.edu.za", "programme": "Advanced Diploma in Application Development", "twoFactorEnabled": false }
   }
   ```

3. Send the access token on every other request:

   ```http
   GET /api/v1/assessments
   Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
   ```

## Tokens

| Token | Lasts | Use |
| --- | --- | --- |
| `accessToken` | 60 minutes | `Authorization: Bearer <accessToken>` on every request |
| `refreshToken` | 30 days | `POST /auth/refresh` to get a new pair when the access token expires |

- **Refresh tokens work once.** Every refresh returns a new refresh token; save it straight away and throw the old one away.
  Sending an old refresh token again counts as theft, and every session for that student is ended.
- **Only one refresh at a time.** If several requests fail with `token_expired` together, refresh once and retry them all
  (the OkHttp `Authenticator` below does this).
- **When refresh fails** (401 `invalid_refresh_token`), go back to the login screen.
- **Logging out:** call `POST /auth/logout` with the refresh token, then delete both tokens.
- **Changes on the website end app sessions.** Changing the password, an admin password reset, or turning two-factor
  off all end every app session.
- **Storage:** keep the refresh token encrypted (Android Keystore, for example Tink with DataStore). Never put tokens
  in plain SharedPreferences or in logs.

## Errors

Every error is a JSON problem (`Content-Type: application/problem+json`). `title` is a sentence you can show to the
student; `code` is a fixed word to switch on.

```json
{ "status": 401, "title": "Enter the 6-digit code from your authenticator app.", "code": "two_factor_required" }
```

| Status | `code` | When | What the app should do |
| --- | --- | --- | --- |
| 400 | `missing_fields` | Login without email or password | Show the message |
| 401 | `invalid_login` | Wrong email or password | Show the message |
| 401 | `two_factor_required` | The student has an authenticator app | Ask for the 6-digit code, then log in again with `code` |
| 401 | `invalid_code` | Wrong or reused authenticator code | Ask again |
| 403 | `students_only` | A lecturer or admin tried to log in | Show the message |
| 403 | `password_change_required` | An admin reset the password | Tell them to log in on the website once |
| 429 | `too_many_attempts` | Too many log-ins from this network in a minute | Wait (see the `Retry-After` header) |
| 401 | `unauthorized` | No token, or a bad token | Go to the login screen |
| 401 | `token_expired` | The access token ran out | Refresh, then retry |
| 401 | `invalid_refresh_token` | The refresh token is unknown, used, expired or revoked | Go to the login screen |
| 404 | `not_found` | Not one of the student's records | Show the message |
| 400 | `invalid_link` | Submission link isn't http(s) | Show the message |
| 400 | `invalid_file` | The upload isn't a real PDF, DOCX or ZIP, is empty, or is too big | Show the message |
| 400 | `missing_file` | The upload had no `file` part | Pick a file first |
| 409 | `submissions_closed` | The assessment's late window has passed | Show the message; hide the hand-in button (see `submissionsOpen`) |
| 400 | `invalid_module`, `invalid_subject`, `invalid_description` | Bad ticket | Show the message by the field |
| 404 / 400 / 403 / 409 | Scan codes (see Attendance) | Scan didn't count | Show the message |
| 500 | `server_error` | Something broke on the server | Ask them to try again later |

## Formats

- **Names:** JSON property names are camelCase.
- **Due dates:** plain dates, for example `"dueDate": "2026-10-10"`. A due date counts for the whole day, college time.
- **Other times:** UTC with an offset, for example `"submittedAt": "2026-10-01T09:00:00+00:00"`. Convert them to the phone's time zone to show them.
- **Numbers:** scores and percentages are JSON numbers, for example `61.5`.
- **Status words:** submission `status` is `not_submitted`, `submitted` or `late`; ticket `status` is `open`, `in_progress` or `resolved`.

## Endpoints

### Auth (no token needed)

| Method and path | Body | Returns |
| --- | --- | --- |
| `POST /auth/login` | `{ "login", "password", "code"? }` | `TokenResponse` |
| `POST /auth/refresh` | `{ "refreshToken" }` | `TokenResponse` (a new pair) |
| `POST /auth/logout` | `{ "refreshToken" }` | 204 |

### The student (Bearer token)

| Method and path | Returns |
| --- | --- |
| `GET /me` | `{ studentId, name, email, programme, twoFactorEnabled }` |
| `GET /modules` | One item per module, with progress and risk (see below) |
| `GET /marks` | Released marks, newest first |
| `GET /attendance` | `[{ moduleId, moduleCode, moduleName, attended, total, percentage }]` |
| `GET /announcements` | `[{ announcementId, title, body, moduleCode?, authorName, createdAt }]`, newest first |

`GET /modules` item:

```json
{
  "moduleId": 8, "code": "XADAD7112", "name": "Work Integrated Learning 3", "term": "Term 2",
  "course": "ADAD0701", "lecturer": "Edward Nkata",
  "average": 74.2, "attendancePercent": 83, "sessionsAttended": 5, "sessionsHeld": 6,
  "submitted": 2, "assessmentCount": 3,
  "atRisk": false, "riskReasons": [],
  "nextDue": { "assessmentId": 24, "name": "POE Part 1", "dueDate": "2026-10-10" }
}
```

`average` counts released marks only; it is `null` until there is one. `attendancePercent` is `null` until a class has been held.

`GET /marks` item:

```json
{ "markId": 31, "moduleId": 8, "moduleCode": "XADAD7112", "assessmentId": 22, "name": "ICE Task 1",
  "score": 15, "maxScore": 20, "percentage": 75, "feedback": "Excellent work.", "capturedOn": "2026-09-12" }
```

### Assessments (Bearer token)

| Method and path | Body | Returns |
| --- | --- | --- |
| `GET /assessments?moduleId=8` | (`moduleId` is optional) | Assessments, soonest first |
| `GET /assessments/{id}` | | One assessment |
| `PUT /assessments/{id}/submission` | `{ "link": "https://github.com/me/poe" }` | The updated assessment |
| `POST /assessments/{id}/files` | `multipart/form-data` with one part named `file` (PDF, DOCX or ZIP) | The updated assessment |
| `GET /files/{fileId}` | | The file (see below) |

```json
{
  "assessmentId": 24, "moduleId": 8, "moduleCode": "XADAD7112", "name": "POE Part 1", "type": "POE",
  "description": "First part of the portfolio of evidence.", "dueDate": "2026-10-10", "maxScore": 100,
  "status": "submitted", "submittedAt": "2026-10-01T09:00:00+00:00", "link": "https://github.com/me/poe",
  "marksReleased": false, "mark": null,
  "file": { "fileId": 31, "name": "POE Part 1.pdf", "sizeBytes": 482113, "uploadedAt": "2026-10-01T09:00:00+00:00" },
  "fileCount": 2, "lastDayToSubmit": "2026-10-13", "submissionsOpen": true
}
```

- `mark` is `null` until the lecturer releases the marks; then it is `{ score, maxScore, percentage, feedback }`.
- Handing in again replaces the link. Work handed in after the due date shows as `late`.
- **Files:** a PDF, Word (.docx) or ZIP file of up to 20 MB (the live site's `Submissions__MaxFileMegabytes`). The site
  reads the file's first bytes, so a renamed file is refused with `invalid_file`. Uploading again keeps the earlier
  files; `file` is always the newest one, the one that counts, and `fileCount` says how many there are. Uploading
  doesn't change the link, and the link endpoint doesn't touch the files.
- **Late window:** `lastDayToSubmit` is the last day work is accepted (`null` means late work is always accepted,
  marked `late`). Once `submissionsOpen` is `false`, both hand-in endpoints answer `409 submissions_closed`.
- **Downloading:** `GET /files/{fileId}` with the Bearer token. On Azure it answers `302` with a link to Azure Storage
  that works for 5 minutes; OkHttp follows it and drops the `Authorization` header because the host changes. Without
  Azure Storage it sends the file itself. Either way the response is the file, with its name in `Content-Disposition`.
  Only the student's own files open; anything else is `404 not_found`.

### Attendance (Bearer token)

`POST /attendance/scan`, body `{ "code": "ABC123", "latitude": -26.1456, "longitude": 28.0438, "accuracy": 12 }` (location optional).

- **The QR code:** the lecturer's QR holds a website link such as `https://<site>/Attendance/Scan?code=ABC123`.
  Read the `code` query parameter. Students can also type the 6-character code.
- **When a scan counts:** the session is open, the student is enrolled, and either the phone is on the campus network
  or the location is within the allowed distance of where the lecturer started the register. So send the location
  whenever the student allows it (ask for `ACCESS_FINE_LOCATION`).

| Result | Status | `code` |
| --- | --- | --- |
| Present | 200 | `{ "result": "present", "message": "You're marked present for XADAD7112.", "moduleCode": "XADAD7112" }` |
| Unknown code | 404 | `unknown_code` |
| Session closed | 400 | `session_closed` |
| Not enrolled | 403 | `not_enrolled` |
| Already present | 409 | `already_present` |
| Not on campus Wi-Fi and location too far or missing | 400 | `not_in_class` |

### Tickets (Bearer token)

| Method and path | Body | Returns |
| --- | --- | --- |
| `GET /tickets` | | Newest first: `{ ticketId, moduleId, moduleCode, subject, description, status, response, openedAt, resolvedAt }` |
| `POST /tickets` | `{ "moduleId": 8, "subject": "...", "description": "..." }` | 201 with the ticket |

Subject is up to 150 characters, description up to 1000, and `moduleId` must be one of the student's modules.

### Calendar and notes (Bearer token)

| Method and path | Body | Returns |
| --- | --- | --- |
| `GET /calendar?from=2026-10-01&to=2026-11-01` | (both optional: four weeks from today; at most 92 days, `to` not included) | Entries in date order |
| `POST /calendar/notes` | `{ "date": "2026-10-07", "time": "18:30", "title": "Study group", "details": "Library", "remind": true }` | `201` and the note as an entry |
| `PUT /calendar/notes/{noteId}/done` | `{ "done": true }` | The note as an entry |
| `DELETE /calendar/notes/{noteId}` | | `204` |

```json
[
  { "id": "college-3", "kind": "college", "allDay": true, "date": "2026-10-12", "lastDate": "2026-10-16", "start": null, "end": null,
    "title": "Term 2 assignment week", "location": null, "details": null, "collegeKind": "Assignments", "noteId": null, "done": false },
  { "id": "class-7-20261005", "kind": "class", "allDay": false, "date": "2026-10-05", "lastDate": null,
    "start": "2026-10-05T07:00:00+00:00", "end": "2026-10-05T08:30:00+00:00", "title": "XADAD7112 class", "location": "Room 3.12",
    "details": null, "collegeKind": null, "noteId": null, "done": false }
]
```

- `kind` is `class`, `due`, `closing` (last day of an assessment's late window), `term`, `college` (holidays, exam and
  assignment weeks, breaks, closing dates; `collegeKind` says which) or `note` (the student's own).
- All-day entries have `date` and, when they run over several days, `lastDate`; timed ones also have `start` and `end` (UTC).
- `time` in a note is optional (`"HH:mm"`). With `remind`, the student gets a notification at that time (or 07:00 that day).
- Errors: `400 invalid_range`, `400 invalid_note` (the message names what to fix), `404 not_found` for someone else's note.

### Notifications (Bearer token)

| Method and path | Returns |
| --- | --- |
| `GET /notifications?unreadOnly=false&page=1` | `{ items: [{ notificationId, title, message, url, createdAt, isRead }], unreadCount, page, totalPages }` |
| `POST /notifications/{id}/read` | 204 |
| `POST /notifications/read-all` | 204 |

`url` is a website path (for example `/Marks/MyMarks`). Use it to choose which app screen to open.

## Android code (Kotlin, Retrofit + OkHttp)

Dependencies: `com.squareup.retrofit2:retrofit`, `com.squareup.retrofit2:converter-gson`, `com.squareup.okhttp3:okhttp`.

```kotlin
data class LoginRequest(val login: String, val password: String, val code: String? = null)
data class RefreshRequest(val refreshToken: String)
data class StudentDto(val studentId: Int, val name: String, val email: String, val programme: String?, val twoFactorEnabled: Boolean)
data class TokenResponse(val accessToken: String, val accessTokenExpiresAt: String,
                         val refreshToken: String, val refreshTokenExpiresAt: String, val student: StudentDto)
data class MarkDto(val score: Double, val maxScore: Double, val percentage: Double, val feedback: String?)
data class AssessmentDto(val assessmentId: Int, val moduleId: Int, val moduleCode: String, val name: String, val type: String,
                         val description: String?, val dueDate: String, val maxScore: Double, val status: String,
                         val submittedAt: String?, val link: String?, val marksReleased: Boolean, val mark: MarkDto?,
                         val file: SubmissionFileDto?, val fileCount: Int, val lastDayToSubmit: String?, val submissionsOpen: Boolean)
data class SubmissionFileDto(val fileId: Int, val name: String, val sizeBytes: Long, val uploadedAt: String)
data class CalendarEntryDto(val id: String, val kind: String, val allDay: Boolean, val date: String, val lastDate: String?,
                            val start: String?, val end: String?, val title: String, val location: String?, val details: String?,
                            val collegeKind: String?, val noteId: Int?, val done: Boolean)
data class NoteRequest(val date: String, val time: String?, val title: String, val details: String?, val remind: Boolean)
data class NoteDoneRequest(val done: Boolean)
data class SubmissionRequest(val link: String)
data class ScanRequest(val code: String, val latitude: Double?, val longitude: Double?, val accuracy: Double?)
data class ScanResponse(val result: String, val message: String, val moduleCode: String?)
data class Problem(val status: Int?, val title: String?, val code: String?)

interface IsmltsApi {
    @POST("auth/login") suspend fun login(@Body body: LoginRequest): Response<TokenResponse>
    @POST("auth/refresh") fun refresh(@Body body: RefreshRequest): Call<TokenResponse>   // synchronous, used by the Authenticator
    @POST("auth/logout") suspend fun logout(@Body body: RefreshRequest): Response<Unit>
    @GET("me") suspend fun me(): StudentDto
    @GET("assessments") suspend fun assessments(@Query("moduleId") moduleId: Int? = null): List<AssessmentDto>
    @PUT("assessments/{id}/submission") suspend fun submit(@Path("id") id: Int, @Body body: SubmissionRequest): Response<AssessmentDto>
    @Multipart @POST("assessments/{id}/files") suspend fun upload(@Path("id") id: Int, @Part file: MultipartBody.Part): Response<AssessmentDto>
    @Streaming @GET("files/{fileId}") suspend fun download(@Path("fileId") fileId: Int): Response<ResponseBody>
    @GET("calendar") suspend fun calendar(@Query("from") from: String? = null, @Query("to") to: String? = null): List<CalendarEntryDto>
    @POST("calendar/notes") suspend fun addNote(@Body body: NoteRequest): Response<CalendarEntryDto>
    @PUT("calendar/notes/{id}/done") suspend fun setNoteDone(@Path("id") id: Int, @Body body: NoteDoneRequest): Response<CalendarEntryDto>
    @DELETE("calendar/notes/{id}") suspend fun deleteNote(@Path("id") id: Int): Response<Unit>
    @POST("attendance/scan") suspend fun scan(@Body body: ScanRequest): Response<ScanResponse>
}

// Keeps the tokens; back it with encrypted storage in the real app
interface TokenStore {
    var accessToken: String?
    var refreshToken: String?
    fun clear()
}

class AuthInterceptor(private val tokens: TokenStore) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): okhttp3.Response {
        val token = tokens.accessToken ?: return chain.proceed(chain.request())
        return chain.proceed(chain.request().newBuilder().header("Authorization", "Bearer $token").build())
    }
}

// Runs on a 401: refreshes once (other requests wait), retries with the new token, or gives up so the app shows login
class TokenAuthenticator(private val tokens: TokenStore, private val refreshApi: () -> IsmltsApi) : Authenticator {
    override fun authenticate(route: Route?, response: okhttp3.Response): Request? = synchronized(this) {
        // A failed login or refresh is final; retrying it would loop
        if (response.request.url.encodedPath.contains("/auth/")) return null
        val sentWith = response.request.header("Authorization")?.removePrefix("Bearer ")
        if (sentWith != null && sentWith != tokens.accessToken) {
            // Another request already refreshed while this one waited
            return response.request.newBuilder().header("Authorization", "Bearer ${tokens.accessToken}").build()
        }
        val refresh = tokens.refreshToken ?: return null
        val result = refreshApi().refresh(RefreshRequest(refresh)).execute()
        val body = result.body()
        if (!result.isSuccessful || body == null) { tokens.clear(); return null }
        tokens.accessToken = body.accessToken
        tokens.refreshToken = body.refreshToken   // the old one no longer works
        return response.request.newBuilder().header("Authorization", "Bearer ${body.accessToken}").build()
    }
}

fun buildApi(baseUrl: String, tokens: TokenStore): IsmltsApi {
    lateinit var api: IsmltsApi
    val client = OkHttpClient.Builder()
        .addInterceptor(AuthInterceptor(tokens))
        .authenticator(TokenAuthenticator(tokens) { api })
        .build()
    api = Retrofit.Builder()
        .baseUrl(baseUrl)                    // must end with "/", e.g. "http://10.0.2.2:5269/api/v1/"
        .client(client)
        .addConverterFactory(GsonConverterFactory.create())
        .build()
        .create(IsmltsApi::class.java)
    return api
}

// Reading an error body
fun Response<*>.problem(): Problem? =
    errorBody()?.string()?.let { runCatching { Gson().fromJson(it, Problem::class.java) }.getOrNull() }

// The QR holds https://<site>/Attendance/Scan?code=ABC123; a typed code is used as it is
fun codeFromQr(text: String): String =
    Uri.parse(text).getQueryParameter("code") ?: text.trim().uppercase()
```

Log-in screen flow:

```kotlin
val response = api.login(LoginRequest(email, password, codeOrNull))
if (response.isSuccessful) { /* save both tokens, go to the home screen */ }
else when (response.problem()?.code) {
    "two_factor_required" -> showCodeField()
    "password_change_required" -> showMessage("Log in on the website once to choose a new password.")
    else -> showMessage(response.problem()?.title ?: "Couldn't log in.")
}
```

Handing in a file the student picked (`ActivityResultContracts.OpenDocument` with the types
`application/pdf`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document` and `application/zip`):

```kotlin
suspend fun uploadWork(context: Context, api: IsmltsApi, assessmentId: Int, uri: Uri): String {
    val resolver = context.contentResolver
    val name = resolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME), null, null, null)?.use {
        if (it.moveToFirst()) it.getString(0) else null
    } ?: "work.pdf"
    val bytes = resolver.openInputStream(uri)!!.use { it.readBytes() }   // files are 20 MB at most
    val part = MultipartBody.Part.createFormData("file", name, bytes.toRequestBody("application/octet-stream".toMediaType()))
    val response = api.upload(assessmentId, part)
    return if (response.isSuccessful) "Handed in ${response.body()?.file?.name}."
           else response.problem()?.title ?: "Couldn't upload the file."   // invalid_file, submissions_closed, ...
}
```

Use `withContext(Dispatchers.IO)` around the reading, and keep the file's own name: it is what the lecturer downloads.

## Running against your PC

- **Emulator.** Use `http://10.0.2.2:5269/api/v1/`, where `10.0.2.2` is the PC from inside the emulator. Android blocks
  plain http unless you allow it, so add a debug-only `res/xml/network_security_config.xml` and reference it from the
  debug manifest (`android:networkSecurityConfig="@xml/network_security_config"`):

  ```xml
  <network-security-config>
      <domain-config cleartextTrafficPermitted="true">
          <domain includeSubdomains="false">10.0.2.2</domain>
      </domain-config>
  </network-security-config>
  ```

  The release build talks only to the https live site.
- **Real phone.** Put the phone and PC on the same Wi-Fi, start the web app with
  `dotnet run --project "ISMLTS(WebApp)" --launch-profile http --urls http://0.0.0.0:5269`, allow port 5269 through
  Windows Firewall, and use `http://<PC's IP>:5269/api/v1/`. Add that IP to the network security config as well.
- **Scanning on the emulator.** Set the emulator's location (Extended controls, then Location) near where the
  lecturer started the register; the campus network check can't pass from an emulator.
- **Swagger.** Open `http://localhost:5269/swagger`, call `POST /api/v1/auth/login`, copy the `accessToken`, press
  **Authorize** and paste it.

## Settings on the live site (Azure App Service)

| Setting | Value |
| --- | --- |
| `Jwt__SigningKey` | A random secret of at least 32 characters. Without it the site makes one at start-up, which signs everyone out of the app whenever the site restarts. Never commit it. |
| `Jwt__AccessTokenMinutes`, `Jwt__RefreshTokenDays` | Optional; default to 60 and 30 |

The live site needs the `Phase5ApiTokens` database migration (it adds the refresh token table).
