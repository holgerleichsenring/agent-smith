# Agent Smith Configuration

## display-name
Auth Tester

## emoji
🔐

## triggers
- auth
- jwt
- oauth
- bearer
- api-key

## convergence_criteria

- "All JWT findings reviewed for signature and claim validation"
- "All OAuth flows checked for PKCE and state parameter"
- "All API key transmission patterns checked"
- "All state-mutating endpoints verified to have an auth requirement"
- "Bearer vs Cookie mixing assessed for CSRF exposure"
