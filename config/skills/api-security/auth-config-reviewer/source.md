# Multi-stack examples

## .NET (ASP.NET Core)

```csharp
// BAD — disabled validations
services.AddAuthentication().AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateLifetime = false,            // expired tokens accepted
        ValidateIssuerSigningKey = false,    // unsigned tokens accepted
        ValidateIssuer = false,
        ValidateAudience = false,
    };
});
```

```csharp
// BAD — middleware ordering
app.UseEndpoints(...);            // endpoints mapped FIRST
app.UseAuthentication();          // never reached
app.UseAuthorization();
```

```csharp
// BAD — CORS
app.UseCors(p => p.AllowAnyOrigin().AllowCredentials());
```

## Express (Node.js)

```js
// BAD — passport mounted but never used on routers
app.use(passport.initialize());
app.use("/api/orders", ordersRouter);   // no passport.authenticate(...) chained
```

## FastAPI (Python)

```python
# BAD — dependency declared but include_router omits it
async def get_current_user(token: str = Depends(oauth2_scheme)): ...

router = APIRouter()
@router.post("/items")
async def create(item: Item):     # missing Depends(get_current_user)
    ...
```

## Spring (Java)

```java
// BAD — SecurityFilterChain excludes the API base path
http.authorizeHttpRequests(a -> a.requestMatchers("/api/**").permitAll());
```
