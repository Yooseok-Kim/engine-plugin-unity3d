﻿// Copyright 2013-2016 iFunFactory Inc. All Rights Reserved.
//
// This work is confidential and proprietary to iFunFactory Inc. and
// must not be used, disclosed, copied, or distributed without the prior
// consent of iFunFactory Inc.

using System;
using System.Collections.Generic;


namespace Fun
{
    public enum EncryptionType
    {
        kNoneEncryption = 0,
        kDefaultEncryption = 100,
        kDummyEncryption,
        kIFunEngine1Encryption,
        kIFunEngine2Encryption,
        kChaCha20Encryption,
        kAes128Encryption
    }


    // Abstract class
    abstract class Encryptor
    {
        public static Encryptor Create (EncryptionType type)
        {
            switch (type)
            {
            case EncryptionType.kDummyEncryption:
                return new Encryptor0();

            case EncryptionType.kIFunEngine1Encryption:
            case EncryptionType.kIFunEngine2Encryption:
                FunDebug.LogWarning("'{0}' encryption type is not support.", type);
                return null;

            case EncryptionType.kChaCha20Encryption:
                return new EncryptorChacha20();

            case EncryptionType.kAes128Encryption:
                return new EncryptorAes128();

            default:
                FunDebug.LogWarning("Unknown encryptor: {0}", type);
                FunDebug.Assert(false);
                return null;
            }
        }

        protected Encryptor (EncryptionType type, string name, State initial_state)
        {
            type_ = type;
            name_ = name;
            state_ = initial_state;
        }

        public virtual bool Handshake (string in_header, ref string out_header)
        {
            FunDebug.Assert(false);
            return true;
        }

        public virtual string generatePublicKey (byte[] server_pub_key)
        {
            return "";
        }

        public abstract Int64 Encrypt (ArraySegment<byte> src, ArraySegment<byte> dst, ref string out_header);
        public abstract Int64 Decrypt (ArraySegment<byte> src, ArraySegment<byte> dst, string in_header);

        public EncryptionType type
        {
            get { return type_; }
        }

        public string name
        {
            get { return name_; }
        }

        public State state
        {
            get { return state_; }
        }


        public enum State
        {
            kHandshaking = 0,
            kEstablished
        }


        EncryptionType type_;
        string name_;
        State state_;
    }


    // encryption - dummy
    class Encryptor0 : Encryptor
    {
        public Encryptor0 () : base(EncryptionType.kDummyEncryption, "dummy", State.kEstablished)
        {
        }

        public override Int64 Encrypt (ArraySegment<byte> src, ArraySegment<byte> dst, ref string out_header)
        {
            FunDebug.Assert(state == State.kEstablished);

            if (dst.Count < src.Count)
                return -1;

            if (!src.Equals(dst))
                dst = new ArraySegment<byte>(src.Array, 0, src.Count);

            return src.Count;
        }

        public override Int64 Decrypt (ArraySegment<byte> src, ArraySegment<byte> dst, string in_header)
        {
            if (in_header.Length > 0)
            {
                FunDebug.LogWarning("Wrong encryptor header.");
                return -1;
            }

            string out_header = "";
            return Encrypt(src, dst, ref out_header);
        }
    }


    // encryption - chacha20
    class EncryptorChacha20 : Encryptor
    {
        public EncryptorChacha20 () : base(EncryptionType.kChaCha20Encryption, "chacha20", State.kEstablished)
        {
        }

        public override Int64 Encrypt (ArraySegment<byte> src, ArraySegment<byte> dst, ref string out_header)
        {
            FunDebug.Assert(state == State.kEstablished);

            return encrypt(src, dst);
        }

        public override Int64 Decrypt (ArraySegment<byte> src, ArraySegment<byte> dst, string in_header)
        {
            FunDebug.Assert(state == State.kEstablished);

            if (in_header.Length > 0)
            {
                FunDebug.LogWarning("Wrong encryptor header.");
                return -1;
            }

            return decrypt(src, dst);
        }

        private Int64 encrypt (ArraySegment<byte> src, ArraySegment<byte> dst)
        {
            Sodium.StreamChacha20XorIc(dst, src, enc_nonce_, enc_key_, enc_idx_);
            enc_idx_ += (ulong)src.Count;
            return src.Count;
        }

        private Int64 decrypt (ArraySegment<byte> src, ArraySegment<byte> dst)
        {
            Sodium.StreamChacha20XorIc(dst, src, dec_nonce_, enc_key_, dec_idx_);
            dec_idx_ += (ulong)src.Count;
            return src.Count;
        }

        public override string generatePublicKey (byte[] server_pub_key)
        {
            byte[] client_pub_key;
            if (Sodium.GenerateChacha20Secrets(server_pub_key, out client_pub_key,
                                               out enc_key_, out enc_nonce_, out dec_nonce_))
            {
                return Sodium.Hexify(client_pub_key);
            }

            return "";
        }


        byte[] enc_key_;
        byte[] enc_nonce_;
        byte[] dec_nonce_;
        ulong enc_idx_;
        ulong dec_idx_;
    }


    // encryption - aes128
    class EncryptorAes128 : Encryptor
    {
        public EncryptorAes128 () : base(EncryptionType.kAes128Encryption, "aes128", State.kEstablished)
        {
        }

        public override Int64 Encrypt (ArraySegment<byte> src, ArraySegment<byte> dst, ref string out_header)
        {
            FunDebug.Assert(state == State.kEstablished);

            return encrypt(src, dst);
        }

        public override Int64 Decrypt (ArraySegment<byte> src, ArraySegment<byte> dst, string in_header)
        {
            FunDebug.Assert(state == State.kEstablished);

            if (in_header.Length > 0)
            {
                FunDebug.LogWarning("Wrong encryptor header.");
                return -1;
            }

            return decrypt(src, dst);
        }

        Int64 encrypt (ArraySegment<byte> src, ArraySegment<byte> dst)
        {
            Sodium.StreamAes128XorTable(dst, src, enc_nonce_, enc_table_);
            Sodium.Increment(enc_nonce_);
            return src.Count;
        }

        Int64 decrypt (ArraySegment<byte> src, ArraySegment<byte> dst)
        {
            Sodium.StreamAes128XorTable(dst, src, dec_nonce_, enc_table_);
            Sodium.Increment(dec_nonce_);
            return dst.Count;
        }

        public override string generatePublicKey (byte[] server_pub_key)
        {
            byte[] client_pub_key;
            if (Sodium.GenerateAes128Secrets(server_pub_key, out client_pub_key,
                                             out enc_table_, out enc_nonce_, out dec_nonce_))
            {
                return Sodium.Hexify(client_pub_key);
            }

            return "";
        }


        byte[] enc_table_;
        byte[] enc_nonce_;
        byte[] dec_nonce_;
    }


    public class FunapiEncryptor : FunDebugLog
    {
        public FunapiEncryptor ()
        {
            public_key = kDefaultPublicKey;

            setDebugObject(this);
        }

        bool createEncryptor (EncryptionType type)
        {
            Encryptor encryptor = Encryptor.Create(type);
            if (encryptor == null)
            {
                LogWarning("Failed to create encryptor: {0}", type);
                return false;
            }

            encryptors_[type] = encryptor;

            if (default_encryptor_ == EncryptionType.kNoneEncryption)
                setDefaultEncryption(type);

            return true;
        }

        void setDefaultEncryption (EncryptionType type)
        {
            if (default_encryptor_ == type)
                return;

            default_encryptor_ = type;
            Log("Set default encryption: {0}", (int)type);
        }

        protected void setEncryption (EncryptionType type)
        {
            if (type == EncryptionType.kIFunEngine1Encryption || type == EncryptionType.kIFunEngine2Encryption)
            {
                LogWarning("'{0}' encryption type is not support.", type);
                FunDebug.Assert(false);
                return;
            }

            if (!createEncryptor(type))
                return;

            setDefaultEncryption(type);
        }

        protected bool hasEncryption (EncryptionType type)
        {
            return encryptors_.ContainsKey(type);
        }

        protected EncryptionType getEncryption (FunapiMessage message)
        {
            if (message.enc_type != EncryptionType.kDefaultEncryption)
                return message.enc_type;

            return default_encryptor_;
        }

        protected void parseEncryptionHeader (ref string encryption_type, ref string encryption_header)
        {
            int index = encryption_header.IndexOf(kDelim1);
            if (index != -1)
            {
                encryption_type = encryption_header.Substring(0, index);
                encryption_header = encryption_header.Substring(index + 1);
            }
            else if (encryption_header != " ") // for HTTP header's blank
            {
                encryption_type = encryption_header;
            }
        }

        protected bool doHandshaking (string encryption_type, string encryption_header)
        {
            if (encryption_type == kEncryptionHandshakeBegin)
            {
                // encryption list
                List<EncryptionType> encryption_list = new List<EncryptionType>();

                if (encryption_header.Length > 0)
                {
                    int begin = 0;
                    int end = encryption_header.IndexOf(kDelim2);
                    EncryptionType type;

                    while (end != -1)
                    {
                        type = (EncryptionType)Convert.ToInt32(encryption_header.Substring(begin, end - begin));
                        encryption_list.Add(type);
                        begin = end + 1;
                        end = encryption_header.IndexOf(kDelim2, begin);
                    }

                    type = (EncryptionType)Convert.ToInt32(encryption_header.Substring(begin));
                    encryption_list.Add(type);
                }

                // Create encryptors
                foreach (EncryptionType type in encryption_list)
                {
                    if (!createEncryptor(type))
                        return false;
                }
            }
            else
            {
                // Encryption handshake message
                EncryptionType type = (EncryptionType)Convert.ToInt32(encryption_type);
                Encryptor encryptor = encryptors_[type];
                if (encryptor == null)
                {
                    Log("Unknown encryption: {0}", encryption_type);
                    return false;
                }

                if (encryptor.state != Encryptor.State.kHandshaking)
                {
                    Log("Unexpected handshake message: {0}", encryptor.name);
                    return false;
                }

                string out_header = "";
                if (!encryptor.Handshake(encryption_header, ref out_header))
                {
                    Log("Encryption handshake failure: {0}", encryptor.name);
                    return false;
                }

                FunDebug.Assert(encryptor.state == Encryptor.State.kEstablished);
            }

            bool handshake_complete = true;
            foreach (KeyValuePair<EncryptionType, Encryptor> pair in encryptors_)
            {
                if (pair.Value.state != Encryptor.State.kEstablished)
                {
                    handshake_complete = false;
                    break;
                }
            }

            return handshake_complete;
        }

        protected bool encryptMessage (FunapiMessage message, EncryptionType type, ref string header)
        {
            if (!encryptors_.ContainsKey(type))
            {
                Log("Unknown encryption: {0}", type);
                return false;
            }

            Encryptor encryptor = encryptors_[type];
            if (encryptor == null || encryptor.state != Encryptor.State.kEstablished)
            {
                Log("Invalid encryption: {0}", type);
                return false;
            }

            if (message.buffer.Count > 0)
            {
                Int64 nSize = encryptor.Encrypt(message.buffer, message.buffer, ref header);
                if (nSize <= 0)
                {
                    Log("Failed to encrypt.");
                    return false;
                }

                FunDebug.Assert(nSize == message.buffer.Count);
            }

            return true;
        }

        protected bool decryptMessage (ArraySegment<byte> buffer, string encryption_type, string encryption_header)
        {
            EncryptionType type = (EncryptionType)Convert.ToInt32(encryption_type);
            if (!encryptors_.ContainsKey(type))
            {
                Log("Unknown encryption: {0}", type);
                return false;
            }

            Encryptor encryptor = encryptors_[type];
            if (encryptor == null)
            {
                Log("Invalid encryption: {0}", type);
                return false;
            }

            Int64 nSize = encryptor.Decrypt(buffer, buffer, encryption_header);
            if (nSize <= 0)
            {
                Log("Failed to decrypt.");
                return false;
            }

            return true;
        }

        // return value: client public key
        protected string generatePublicKey (EncryptionType type)
        {
            if (!encryptors_.ContainsKey(type))
            {
                Log("Unknown encryption: {0} requested public key", type);
                return "";
            }

            Encryptor encryptor = encryptors_[type];
            if (encryptor == null)
            {
                Log("Invalid encryption: {0}", type);
                return "";
            }

            return encryptor.generatePublicKey(pub_key_);
        }

        public static string public_key
        {
            set
            {
                if (value.Length != 64)
                {
                    throw new ArgumentException("public key's length is invalid. The length should be 64 bytes.",
                                                "FunapiEncryptor.public_key");
                }

                pub_key_ = Sodium.Unhexify(value);
            }
        }


        const string kDefaultPublicKey = "0b8504a9c1108584f4f0a631ead8dd548c0101287b91736566e13ead3f008f5d";

        const string kEncryptionHandshakeBegin = "HELLO!";
        const char kDelim1 = '-';
        const char kDelim2 = ',';

        EncryptionType default_encryptor_ = EncryptionType.kNoneEncryption;
        Dictionary<EncryptionType, Encryptor> encryptors_ = new Dictionary<EncryptionType, Encryptor>();
        static byte[] pub_key_ = null;
    }
}
