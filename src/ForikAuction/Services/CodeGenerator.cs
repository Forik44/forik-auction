using System.Security.Cryptography;

namespace ForikAuction.Services;

public static class CodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // без похожих символов
    public static string NewJoinCode(int len = 6)
    {
        var sb = new char[len];
        for (int i = 0; i < len; i++) sb[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(sb);
    }
}
