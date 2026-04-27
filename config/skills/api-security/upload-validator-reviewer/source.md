# Multi-stack examples

## .NET (ASP.NET Core IFormFile)

```csharp
// BAD — header-only MIME
[HttpPost("avatar")]
public async Task<IActionResult> Upload(IFormFile file)
{
    if (file.ContentType != "image/png") return BadRequest();
    var path = Path.Combine("wwwroot/avatars", file.FileName); // path traversal
    await using var stream = System.IO.File.Create(path);
    await file.CopyToAsync(stream);
    return Ok();
}

// GOOD — magic bytes + sanitized name + size limit
[RequestSizeLimit(2_000_000)]
[HttpPost("avatar")]
public async Task<IActionResult> Upload(IFormFile file)
{
    if (!await IsPngAsync(file)) return BadRequest();
    var safeName = $"{Guid.NewGuid():N}.png";
    ...
}
```

## Express + Multer

```js
// BAD — accepts everything, original filename
const upload = multer({ dest: "public/uploads/" });
app.post("/avatar", upload.single("file"), (req, res) => res.send("ok"));

// GOOD
const upload = multer({
  storage: multer.memoryStorage(),
  limits: { fileSize: 2 * 1024 * 1024 },
  fileFilter: (req, file, cb) => cb(null, file.mimetype === "image/png")
});
// then validate magic bytes from req.file.buffer before persisting
```

## FastAPI (Python)

```python
# BAD
@app.post("/avatar")
async def upload(file: UploadFile = File(...)):
    with open(f"static/{file.filename}", "wb") as f:    # path traversal
        f.write(await file.read())

# GOOD
@app.post("/avatar")
async def upload(file: UploadFile = File(..., max_size=2_000_000)):
    head = await file.read(8)
    if head[:8] != b"\x89PNG\r\n\x1a\n": raise HTTPException(400)
    ...
```

## Spring (MultipartFile)

```java
// BAD
@PostMapping("/avatar")
public void upload(@RequestParam MultipartFile file) throws IOException {
    Files.copy(file.getInputStream(), Paths.get("public", file.getOriginalFilename()));
}
```
