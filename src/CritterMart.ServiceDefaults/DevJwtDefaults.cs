namespace CritterMart.ServiceDefaults;

// The DEV-ONLY JWT defaults shared across the auth issuer (Identity) and the resource servers (ADR 023).
// Identity signs with the PRIVATE key; the resource servers validate with the PUBLIC key. Keeping both
// halves of the SAME keypair here — the one place both sides reference — means the demo works with ZERO
// key wiring (Identity mints with DevPrivateKeyPem, Orders verifies with DevPublicKeyPem, they match by
// construction) and the two halves can never drift out of sync across services.
//
// !!! DEV ONLY — NOT A SECRET !!!
// This private key signs nothing of value in a local demo. Production MUST override via the `Jwt:PrivateKey`
// (Identity) and `Jwt:PublicKey` (resource servers) configuration keys with real key material from a secret
// store / user-secrets. Rotation = redeploy that config (ADR 023's accepted round-one tradeoff — the price
// of validating offline against a config public key instead of a fetched JWKS document; Workshop 002 § 8
// item 17). A committed dev private key is a deliberate, loudly-marked demo affordance, the auth analogue of
// the config-driven demo deadlines (EmailChangeDeadline / PaymentDeadline).
public static class DevJwtDefaults
{
    // The issuer + audience the dev token is stamped with, and the resource servers validate against.
    public const string Issuer = "crittermart-identity";
    public const string Audience = "crittermart";

    // RSA-2048 keypair (PKCS#8 private / SPKI public), generated once for the demo. Not sensitive.
    public const string DevPrivateKeyPem = """
        -----BEGIN PRIVATE KEY-----
        MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQCnXZgWHdFDC007
        4skH8UILqRdr18WpxfTJi8sdKxk+v9nXTSrjz89wiaMh1PheuL+0lEQ03HJeS2Hs
        qCd/lTon+XgJhFAGcAApVb6jl/dMonR4ZkecNFpRCqSQi7W6QAgiy1vCu2K9a5WW
        0ouYzZFVxNzC/OKqastTsRGyunf4WGUyLz5iYzY7Hvw50uqOtijh7k93waBHXVum
        H5mN+EHJa8zANawcpHz6FVtuYqRW35sPqaXRttq82NPkHTFU3x4fQEVrfzh5fxwJ
        E07FGpE10ceAh6WXWDVTFm/Ne5zJS9vj+CH+0iDLtGxli57rIrhOozEcg2qPg2Oq
        CrEAdNQBAgMBAAECggEAGk1P0g9MNwMXAir5I4cQtWW/c91BsmqVC6TX03UAefy1
        2WtxKsLFJjnF1Kf0Lb6kXKv9rr5Y4uS/NkKUN9gAfw5qL49cRtwMdR0v4TnH+C0t
        Afbg7iAeyXmotGagX+122dunnLTM0a6PSwKPmatEryae+FhnBBfVwwiixWR0kic2
        ZFUwwcxQbdSys6dhOutL436tl3tscfHNSbNnAMXFCl9I7SXHzjVjeEcvp2BoR4kA
        gOrDwb03B11SIIs1davXPqztmqMYJwE6Y1yOfjX184krwDgiMUiqJSHbykhIb5kl
        VpOpQ8Yb3o+AyLeCBILY93QaHkeaR2tsj2tTLUqFGQKBgQDVIs1wSkBRix5ZB+lc
        fgLofd1uqYg6WBHil2bpaFLu98XKms44abV1z+ZbnbPQB8XkUeLAAHfhBW191/ve
        2RsNa/RschAOCPf9ZryvOSjMT9TKpa5JfjmVfBRZARXijDhCkuVXV3aZ9/TdHk4T
        iNWdx9zPXwKh4BdQvZmIoOmhFwKBgQDJBlQKeECqRbk0aVKTB2yS/hN4JC8gTdMB
        umBQ3IiPvuYjPleJeRsH867X8uIbg7ariqQ7U3NaTxksxfQRUoW4Ye4ZtcrD6hsC
        4VVEnvkKJKFrYTTvffKCy+wcKe/XtENZS2ofdbX6aElgpJvYq0X9wJi1CAt3Zbkp
        PWi5mYPypwKBgQC/mNOZWAZNx4P2gPg1H0o5+buvGVPPLxCU44mt1QyIqc/yfAta
        Bx0K1WO9hBz6q6Inx7zQ4Rri++Abuqc/A2ggPqWxPzBTjZhxAYQo+HdGg5VEvn/Y
        rVHSoYIhKKqlx2tj3W2xgHyrmI1UoUOKp/1wIxTKjhxtrGcJPAfjHNQo7QKBgDn8
        +lc+0yCLFl7ZFvnUxWwtoL4iafm+mWTBN7F7vGUC4249OJEufy6vC7u9k53uQ85+
        Itv+OaNOd+ujesFYdbx3e3CtMT2MlZgiGi++UAauBGZuVw/S3BcA7i49prMpi9gB
        Wi6TDRib5rbbJR2+YmVNnn9yP6SEkoIj9ca8UwS3AoGAIboW8LGB1ZkacQN5fcfP
        waEv1+D4DEymBbFMjHUGRFPZN3lwGiwRxUc7HGjpvzxuORbjk5CfPQ58oxxsglW+
        00QiA9jmJjKHThrCLlK+cPJHSezVJ42LK0lAejhMHSIOzOJnc5j9Mk+OzWP2Tdhj
        7Yzn/m+VAY+UrHInb1ue7Qw=
        -----END PRIVATE KEY-----
        """;

    public const string DevPublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAp12YFh3RQwtNO+LJB/FC
        C6kXa9fFqcX0yYvLHSsZPr/Z100q48/PcImjIdT4Xri/tJRENNxyXkth7Kgnf5U6
        J/l4CYRQBnAAKVW+o5f3TKJ0eGZHnDRaUQqkkIu1ukAIIstbwrtivWuVltKLmM2R
        VcTcwvziqmrLU7ERsrp3+FhlMi8+YmM2Ox78OdLqjrYo4e5Pd8GgR11bph+ZjfhB
        yWvMwDWsHKR8+hVbbmKkVt+bD6ml0bbavNjT5B0xVN8eH0BFa384eX8cCRNOxRqR
        NdHHgIell1g1UxZvzXucyUvb4/gh/tIgy7RsZYue6yK4TqMxHINqj4NjqgqxAHTU
        AQIDAQAB
        -----END PUBLIC KEY-----
        """;
}
