using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace Web.Features.DingTalk;

/// <summary>
/// 钉钉事件订阅加解密工具类（100% 对齐官方协议）
///
/// 官方协议：
/// - AES-CBC-256, IV 为 aesKey 前 16 字节
/// - 明文结构：16字节随机串 + 4字节网络序长度 + 内容(bytes) + receiveId(appKey/corpId)
/// - 签名：token,timestamp,nonce,encrypt 4个字符串排序后拼接 SHA1
/// </summary>
public static class DingTalkCrypto
{
    private static readonly byte[] _aesKey;
    private static readonly byte[] _iv;

    // 你提供的配置（也可以后续改成从 appsettings 读取）
    private const string Token = "jJ4WcUWaKV0kmfKyVkdoLqzljUBLS1leW";
    private const string AesKeyBase64 = "4k9kDWCkklIptR07RQcdV4V5vPewScJ0IMfm3tYatUE";

    // 钉钉事件订阅的 receiveId 可能是以下之一：
    // 1. 企业自建应用：填 suiteKey（即 AppKey）
    // 2. 第三方企业应用：填 suiteKey
    // 3. 旧版：可能填企业ID（CorpId）
    // 这里先用 AppKey，如果还报错再改成企业ID
    private const string ReceiveId = "dingjcvvpwey1h7grkjf"; // 你的 AppKey

    static DingTalkCrypto()
    {
        // 钉钉 aes_key 是 43 位 Base64，解码时需要补 1 个 '='
        _aesKey = Convert.FromBase64String(AesKeyBase64 + "=");
        _iv = _aesKey[..16];
    }

    public static bool VerifySignature(string signature, string timestamp, string nonce, string encrypt)
    {
        var sign = ComputeSignature(timestamp, nonce, encrypt);
        return string.Equals(sign, signature, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 解密 encrypt 字段，返回内容（content）。
    /// 同时校验 receiveId 是否匹配。
    /// </summary>
    public static string DecryptMsg(string base64Encrypt)
    {
        var plain = AesDecrypt(base64Encrypt);

        if (plain.Length < 20)
            throw new CryptographicException("Plaintext too short.");

        // 16 bytes random
        var len = BinaryPrimitives.ReadInt32BigEndian(plain.AsSpan(16, 4));
        if (len < 0 || 20 + len > plain.Length)
            throw new CryptographicException("Invalid plaintext length.");

        var contentBytes = plain.AsSpan(20, len).ToArray();
        var receiveIdBytes = plain.AsSpan(20 + len).ToArray();

        var content = Encoding.UTF8.GetString(contentBytes);
        var rid = Encoding.UTF8.GetString(receiveIdBytes);

        if (!string.Equals(rid, ReceiveId, StringComparison.Ordinal))
            throw new CryptographicException($"ReceiveId mismatch. got={rid}");

        return content;
    }

    /// <summary>
    /// 按官方协议加密 content，并生成 msg_signature。
    /// </summary>
    public static string EncryptMsg(string content, string timestamp, string nonce, out string msgSignature)
    {
        var random16 = RandomNumberGenerator.GetBytes(16);
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var receiveIdBytes = Encoding.UTF8.GetBytes(ReceiveId);

        var buf = new byte[16 + 4 + contentBytes.Length + receiveIdBytes.Length];
        random16.CopyTo(buf, 0);
        BinaryPrimitives.WriteInt32BigEndian(buf.AsSpan(16, 4), contentBytes.Length);
        contentBytes.CopyTo(buf, 20);
        receiveIdBytes.CopyTo(buf, 20 + contentBytes.Length);

        var encrypt = AesEncrypt(buf);
        msgSignature = ComputeSignature(timestamp, nonce, encrypt);
        return encrypt;
    }

    private static string ComputeSignature(string timestamp, string nonce, string encrypt)
    {
        var arr = new[] { Token, timestamp ?? string.Empty, nonce ?? string.Empty, encrypt ?? string.Empty };
        Array.Sort(arr, StringComparer.Ordinal);
        var raw = string.Concat(arr[0], arr[1], arr[2], arr[3]);

        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string AesEncrypt(byte[] plain)
    {
        using var aes = Aes.Create();
        aes.Key = _aesKey;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var cipherBytes = encryptor.TransformFinalBlock(plain, 0, plain.Length);
        return Convert.ToBase64String(cipherBytes);
    }

    private static byte[] AesDecrypt(string base64Encrypted)
    {
        using var aes = Aes.Create();
        aes.Key = _aesKey;
        aes.IV = _iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var encryptedBytes = Convert.FromBase64String(base64Encrypted);
        return decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
    }
}
