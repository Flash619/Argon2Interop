﻿using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Argon2.Interop;

public class Argon2
{
    private readonly Argon2Options _options;

    public Argon2()
    {
        _options = new Argon2Options();
    }
    
    public Argon2(Argon2Options options)
    {
        _options = options;
    }

    public string Hash(string password)
        => Hash(Encoding.UTF8.GetBytes(password));

    public string Hash(string password, string salt)
        => Hash(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(salt));

    public string Hash(byte[] password)
        => Hash(password, GenerateSalt(password.Length));

    public string Hash(byte[] password, byte[] salt)
    {
        Hash(password, salt, out var hash, out var encoded);

        return encoded;
    }

    public void Hash(string password, out byte[] hash, out string encoded)
    {
        Hash(Encoding.UTF8.GetBytes(password), out var outHash, out var outEncoded);

        hash = outHash;
        encoded = outEncoded;
    }

    public void Hash(string password, string salt, out byte[] hash, out string encoded)
    {
        Hash(Encoding.UTF8.GetBytes(password), Encoding.UTF8.GetBytes(salt), out var outHash, out var outEncoded);

        hash = outHash;
        encoded = outEncoded;
    }

    public void Hash(byte[] password, out byte[] hash, out string encoded)
    {
        Hash(password, GenerateSalt(Math.Max(Argon2Constants.MinSaltLength, password.Length)), out var outHash, out var outEncoded);

        hash = outHash;
        encoded = outEncoded;
    }

    public void Hash(byte[] password, byte[] salt, out byte[] hash, out string encoded)
    {
        var hashBuffer = new byte[_options.HashLength];
        var expectedArgonTemplate = $"$argon2{_options.Type}$v={(uint)_options.Version}$m={_options.MemoryCost},t={_options.TimeCost},p={_options.Parallelism}$$";
        // We are able to determine our expected hash length, however Argon2 requires the encoded array to be padded with at least 1 extra byte. We remove it later on.
        var encodedBuffer = new byte[expectedArgonTemplate.Length + Base645Len(_options.HashLength) + Base645Len(salt.Length) + 1];
        var error = argon2_hash(
            timeCost: (uint) _options.TimeCost, 
            memoryCost: (uint) _options.MemoryCost,
            parallelism: (uint) _options.Parallelism, 
            password: password, 
            passwordLength: (nuint) password.Length, 
            salt: salt, 
            saltLength: (nuint) salt.Length,
            hash: hashBuffer, 
            hashLength: (nuint) _options.HashLength, 
            encoded: encodedBuffer,
            encodedLength: (nuint) encodedBuffer.Length, 
            type: (uint) _options.Type, 
            version: (uint) _options.Version);

        CheckArgonResponse(error);

        hash = hashBuffer;
        // Remove padding.
        encoded = Encoding.ASCII.GetString(encodedBuffer.Where(x => x != 0x00).ToArray());
    }

    public bool Verify(string encoded, string password)
        => Verify(encoded, Encoding.UTF8.GetBytes(password));
    
    public bool Verify(string encoded, byte[] password)
    {
        var error = argon2_verify(Encoding.ASCII.GetBytes(encoded), password, (nuint)password.Length, (uint)_options.Type);

        if (error != Argon2Error.None && error != Argon2Error.VerifyMismatch)
        {
            // Fall back to traditional error handling if the error is an actual error other than failed verification.
            CheckArgonResponse(error);
        }
        
        return error == Argon2Error.None;
    }

    [DllImport("libargon2", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern Argon2Error argon2_hash(uint timeCost, uint memoryCost, uint parallelism, byte[] password, nuint passwordLength, byte[] salt, nuint saltLength, byte[] hash, nuint hashLength, byte[] encoded, nuint encodedLength, uint type, uint version);

    [DllImport("libargon2", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern Argon2Error argon2_verify(byte[] encoded, byte[] password, nuint passwordLength, uint type);
    
    private static byte[] GenerateSalt(int length)
    {
        using var rng = RandomNumberGenerator.Create();
        
        var salt = new byte[length];
        
        rng.GetBytes(salt);

        return salt;
    }
    
    private static int Base645Len(int length)
    {
        return (int) Math.Ceiling((double)length / 3 * 4);
    }

    private static void CheckArgonResponse(Argon2Error error)
    {
        if (error != Argon2Error.None)
        {
            throw new CryptographicException($"Failed to generate Argon2 hash. Error: {(int) error} ({error})");
        }
    }
}