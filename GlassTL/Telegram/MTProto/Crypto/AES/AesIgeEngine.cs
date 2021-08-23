﻿namespace GlassTL.Telegram.MTProto.Crypto.AES
{
    using System;
    using Utils;

    /// <summary>
    /// A custom AES Engine that specifically targets IGE cypher mode
    /// </summary>
    public class AesIgeEngine
    {
        // The S box
        private const uint M1 = 0x80808080;
        private const uint M2 = 0x7f7f7f7f;
        private const uint M3 = 0x0000001b;
        private const int BLOCK_SIZE = 0x10;

        private static readonly byte[] S = {
            0x63, 0x7C, 0x77, 0x7B, 0xF2, 0x6B, 0x6F, 0xC5, 0x30, 0x01, 0x67, 0x2B, 0xFE, 0xD7, 0xAB, 0x76,
            0xCA, 0x82, 0xC9, 0x7D, 0xFA, 0x59, 0x47, 0xF0, 0xAD, 0xD4, 0xA2, 0xAF, 0x9C, 0xA4, 0x72, 0xC0,
            0xB7, 0xFD, 0x93, 0x26, 0x36, 0x3F, 0xF7, 0xCC, 0x34, 0xA5, 0xE5, 0xF1, 0x71, 0xD8, 0x31, 0x15,
            0x04, 0xC7, 0x23, 0xC3, 0x18, 0x96, 0x05, 0x9A, 0x07, 0x12, 0x80, 0xE2, 0xEB, 0x27, 0xB2, 0x75,
            0x09, 0x83, 0x2C, 0x1A, 0x1B, 0x6E, 0x5A, 0xA0, 0x52, 0x3B, 0xD6, 0xB3, 0x29, 0xE3, 0x2F, 0x84,
            0x53, 0xD1, 0x00, 0xED, 0x20, 0xFC, 0xB1, 0x5B, 0x6A, 0xCB, 0xBE, 0x39, 0x4A, 0x4C, 0x58, 0xCF,
            0xD0, 0xEF, 0xAA, 0xFB, 0x43, 0x4D, 0x33, 0x85, 0x45, 0xF9, 0x02, 0x7F, 0x50, 0x3C, 0x9F, 0xA8,
            0x51, 0xA3, 0x40, 0x8F, 0x92, 0x9D, 0x38, 0xF5, 0xBC, 0xB6, 0xDA, 0x21, 0x10, 0xFF, 0xF3, 0xD2,
            0xCD, 0x0C, 0x13, 0xEC, 0x5F, 0x97, 0x44, 0x17, 0xC4, 0xA7, 0x7E, 0x3D, 0x64, 0x5D, 0x19, 0x73,
            0x60, 0x81, 0x4F, 0xDC, 0x22, 0x2A, 0x90, 0x88, 0x46, 0xEE, 0xB8, 0x14, 0xDE, 0x5E, 0x0B, 0xDB,
            0xE0, 0x32, 0x3A, 0x0A, 0x49, 0x06, 0x24, 0x5C, 0xC2, 0xD3, 0xAC, 0x62, 0x91, 0x95, 0xE4, 0x79,
            0xE7, 0xC8, 0x37, 0x6D, 0x8D, 0xD5, 0x4E, 0xA9, 0x6C, 0x56, 0xF4, 0xEA, 0x65, 0x7A, 0xAE, 0x08,
            0xBA, 0x78, 0x25, 0x2E, 0x1C, 0xA6, 0xB4, 0xC6, 0xE8, 0xDD, 0x74, 0x1F, 0x4B, 0xBD, 0x8B, 0x8A,
            0x70, 0x3E, 0xB5, 0x66, 0x48, 0x03, 0xF6, 0x0E, 0x61, 0x35, 0x57, 0xB9, 0x86, 0xC1, 0x1D, 0x9E,
            0xE1, 0xF8, 0x98, 0x11, 0x69, 0xD9, 0x8E, 0x94, 0x9B, 0x1E, 0x87, 0xE9, 0xCE, 0x55, 0x28, 0xDF,
            0x8C, 0xA1, 0x89, 0x0D, 0xBF, 0xE6, 0x42, 0x68, 0x41, 0x99, 0x2D, 0x0F, 0xB0, 0x54, 0xBB, 0x16
        };

        // The inverse S-box
        private static readonly byte[] Si = {
            0x52, 0x09, 0x6A, 0xD5, 0x30, 0x36, 0xA5, 0x38, 0xBF, 0x40, 0xA3, 0x9E, 0x81, 0xF3, 0xD7, 0xFB,
            0x7C, 0xE3, 0x39, 0x82, 0x9B, 0x2F, 0xFF, 0x87, 0x34, 0x8E, 0x43, 0x44, 0xC4, 0xDE, 0xE9, 0xCB,
            0x54, 0x7B, 0x94, 0x32, 0xA6, 0xC2, 0x23, 0x3D, 0xEE, 0x4C, 0x95, 0x0B, 0x42, 0xFA, 0xC3, 0x4E,
            0x08, 0x2E, 0xA1, 0x66, 0x28, 0xD9, 0x24, 0xB2, 0x76, 0x5B, 0xA2, 0x49, 0x6D, 0x8B, 0xD1, 0x25,
            0x72, 0xF8, 0xF6, 0x64, 0x86, 0x68, 0x98, 0x16, 0xD4, 0xA4, 0x5C, 0xCC, 0x5D, 0x65, 0xB6, 0x92,
            0x6C, 0x70, 0x48, 0x50, 0xFD, 0xED, 0xB9, 0xDA, 0x5E, 0x15, 0x46, 0x57, 0xA7, 0x8D, 0x9D, 0x84,
            0x90, 0xD8, 0xAB, 0x00, 0x8C, 0xBC, 0xD3, 0x0A, 0xF7, 0xE4, 0x58, 0x05, 0xB8, 0xB3, 0x45, 0x06,
            0xD0, 0x2C, 0x1E, 0x8F, 0xCA, 0x3F, 0x0F, 0x02, 0xC1, 0xAF, 0xBD, 0x03, 0x01, 0x13, 0x8A, 0x6B,
            0x3A, 0x91, 0x11, 0x41, 0x4F, 0x67, 0xDC, 0xEA, 0x97, 0xF2, 0xCF, 0xCE, 0xF0, 0xB4, 0xE6, 0x73,
            0x96, 0xAC, 0x74, 0x22, 0xE7, 0xAD, 0x35, 0x85, 0xE2, 0xF9, 0x37, 0xE8, 0x1C, 0x75, 0xDF, 0x6E,
            0x47, 0xF1, 0x1A, 0x71, 0x1D, 0x29, 0xC5, 0x89, 0x6F, 0xB7, 0x62, 0x0E, 0xAA, 0x18, 0xBE, 0x1B,
            0xFC, 0x56, 0x3E, 0x4B, 0xC6, 0xD2, 0x79, 0x20, 0x9A, 0xDB, 0xC0, 0xFE, 0x78, 0xCD, 0x5A, 0xF4,
            0x1F, 0xDD, 0xA8, 0x33, 0x88, 0x07, 0xC7, 0x31, 0xB1, 0x12, 0x10, 0x59, 0x27, 0x80, 0xEC, 0x5F,
            0x60, 0x51, 0x7F, 0xA9, 0x19, 0xB5, 0x4A, 0x0D, 0x2D, 0xE5, 0x7A, 0x9F, 0x93, 0xC9, 0x9C, 0xEF,
            0xA0, 0xE0, 0x3B, 0x4D, 0xAE, 0x2A, 0xF5, 0xB0, 0xC8, 0xEB, 0xBB, 0x3C, 0x83, 0x53, 0x99, 0x61,
            0x17, 0x2B, 0x04, 0x7E, 0xBA, 0x77, 0xD6, 0x26, 0xE1, 0x69, 0x14, 0x63, 0x55, 0x21, 0x0C, 0x7D
        };

        // Vector used in calculating key schedule (powers of x in GF(256))
        private static readonly byte[] Rcon = {
            0x01, 0x02, 0x04, 0x08, 0x10, 0x20, 0x40, 0x80, 0x1b, 0x36, 0x6c, 0xd8, 0xab, 0x4d, 0x9a,
            0x2f, 0x5e, 0xbc, 0x63, 0xc6, 0x97, 0x35, 0x6a, 0xd4, 0xb3, 0x7d, 0xfa, 0xef, 0xc5, 0x91
        };

        // Precomputation tables of calculations for rounds
        private static readonly uint[] T0 = {
            0xa56363c6, 0x847c7cf8, 0x997777ee, 0x8d7b7bf6, 0x0df2f2ff,
            0xbd6b6bd6, 0xb16f6fde, 0x54c5c591, 0x50303060, 0x03010102,
            0xa96767ce, 0x7d2b2b56, 0x19fefee7, 0x62d7d7b5, 0xe6abab4d,
            0x9a7676ec, 0x45caca8f, 0x9d82821f, 0x40c9c989, 0x877d7dfa,
            0x15fafaef, 0xeb5959b2, 0xc947478e, 0x0bf0f0fb, 0xecadad41,
            0x67d4d4b3, 0xfda2a25f, 0xeaafaf45, 0xbf9c9c23, 0xf7a4a453,
            0x967272e4, 0x5bc0c09b, 0xc2b7b775, 0x1cfdfde1, 0xae93933d,
            0x6a26264c, 0x5a36366c, 0x413f3f7e, 0x02f7f7f5, 0x4fcccc83,
            0x5c343468, 0xf4a5a551, 0x34e5e5d1, 0x08f1f1f9, 0x937171e2,
            0x73d8d8ab, 0x53313162, 0x3f15152a, 0x0c040408, 0x52c7c795,
            0x65232346, 0x5ec3c39d, 0x28181830, 0xa1969637, 0x0f05050a,
            0xb59a9a2f, 0x0907070e, 0x36121224, 0x9b80801b, 0x3de2e2df,
            0x26ebebcd, 0x6927274e, 0xcdb2b27f, 0x9f7575ea, 0x1b090912,
            0x9e83831d, 0x742c2c58, 0x2e1a1a34, 0x2d1b1b36, 0xb26e6edc,
            0xee5a5ab4, 0xfba0a05b, 0xf65252a4, 0x4d3b3b76, 0x61d6d6b7,
            0xceb3b37d, 0x7b292952, 0x3ee3e3dd, 0x712f2f5e, 0x97848413,
            0xf55353a6, 0x68d1d1b9, 0x00000000, 0x2cededc1, 0x60202040,
            0x1ffcfce3, 0xc8b1b179, 0xed5b5bb6, 0xbe6a6ad4, 0x46cbcb8d,
            0xd9bebe67, 0x4b393972, 0xde4a4a94, 0xd44c4c98, 0xe85858b0,
            0x4acfcf85, 0x6bd0d0bb, 0x2aefefc5, 0xe5aaaa4f, 0x16fbfbed,
            0xc5434386, 0xd74d4d9a, 0x55333366, 0x94858511, 0xcf45458a,
            0x10f9f9e9, 0x06020204, 0x817f7ffe, 0xf05050a0, 0x443c3c78,
            0xba9f9f25, 0xe3a8a84b, 0xf35151a2, 0xfea3a35d, 0xc0404080,
            0x8a8f8f05, 0xad92923f, 0xbc9d9d21, 0x48383870, 0x04f5f5f1,
            0xdfbcbc63, 0xc1b6b677, 0x75dadaaf, 0x63212142, 0x30101020,
            0x1affffe5, 0x0ef3f3fd, 0x6dd2d2bf, 0x4ccdcd81, 0x140c0c18,
            0x35131326, 0x2fececc3, 0xe15f5fbe, 0xa2979735, 0xcc444488,
            0x3917172e, 0x57c4c493, 0xf2a7a755, 0x827e7efc, 0x473d3d7a,
            0xac6464c8, 0xe75d5dba, 0x2b191932, 0x957373e6, 0xa06060c0,
            0x98818119, 0xd14f4f9e, 0x7fdcdca3, 0x66222244, 0x7e2a2a54,
            0xab90903b, 0x8388880b, 0xca46468c, 0x29eeeec7, 0xd3b8b86b,
            0x3c141428, 0x79dedea7, 0xe25e5ebc, 0x1d0b0b16, 0x76dbdbad,
            0x3be0e0db, 0x56323264, 0x4e3a3a74, 0x1e0a0a14, 0xdb494992,
            0x0a06060c, 0x6c242448, 0xe45c5cb8, 0x5dc2c29f, 0x6ed3d3bd,
            0xefacac43, 0xa66262c4, 0xa8919139, 0xa4959531, 0x37e4e4d3,
            0x8b7979f2, 0x32e7e7d5, 0x43c8c88b, 0x5937376e, 0xb76d6dda,
            0x8c8d8d01, 0x64d5d5b1, 0xd24e4e9c, 0xe0a9a949, 0xb46c6cd8,
            0xfa5656ac, 0x07f4f4f3, 0x25eaeacf, 0xaf6565ca, 0x8e7a7af4,
            0xe9aeae47, 0x18080810, 0xd5baba6f, 0x887878f0, 0x6f25254a,
            0x722e2e5c, 0x241c1c38, 0xf1a6a657, 0xc7b4b473, 0x51c6c697,
            0x23e8e8cb, 0x7cdddda1, 0x9c7474e8, 0x211f1f3e, 0xdd4b4b96,
            0xdcbdbd61, 0x868b8b0d, 0x858a8a0f, 0x907070e0, 0x423e3e7c,
            0xc4b5b571, 0xaa6666cc, 0xd8484890, 0x05030306, 0x01f6f6f7,
            0x120e0e1c, 0xa36161c2, 0x5f35356a, 0xf95757ae, 0xd0b9b969,
            0x91868617, 0x58c1c199, 0x271d1d3a, 0xb99e9e27, 0x38e1e1d9,
            0x13f8f8eb, 0xb398982b, 0x33111122, 0xbb6969d2, 0x70d9d9a9,
            0x898e8e07, 0xa7949433, 0xb69b9b2d, 0x221e1e3c, 0x92878715,
            0x20e9e9c9, 0x49cece87, 0xff5555aa, 0x78282850, 0x7adfdfa5,
            0x8f8c8c03, 0xf8a1a159, 0x80898909, 0x170d0d1a, 0xdabfbf65,
            0x31e6e6d7, 0xc6424284, 0xb86868d0, 0xc3414182, 0xb0999929,
            0x772d2d5a, 0x110f0f1e, 0xcbb0b07b, 0xfc5454a8, 0xd6bbbb6d,
            0x3a16162c
        };

        private static readonly uint[] Tinv0 = {
            0x50a7f451, 0x5365417e, 0xc3a4171a, 0x965e273a, 0xcb6bab3b,
            0xf1459d1f, 0xab58faac, 0x9303e34b, 0x55fa3020, 0xf66d76ad,
            0x9176cc88, 0x254c02f5, 0xfcd7e54f, 0xd7cb2ac5, 0x80443526,
            0x8fa362b5, 0x495ab1de, 0x671bba25, 0x980eea45, 0xe1c0fe5d,
            0x02752fc3, 0x12f04c81, 0xa397468d, 0xc6f9d36b, 0xe75f8f03,
            0x959c9215, 0xeb7a6dbf, 0xda595295, 0x2d83bed4, 0xd3217458,
            0x2969e049, 0x44c8c98e, 0x6a89c275, 0x78798ef4, 0x6b3e5899,
            0xdd71b927, 0xb64fe1be, 0x17ad88f0, 0x66ac20c9, 0xb43ace7d,
            0x184adf63, 0x82311ae5, 0x60335197, 0x457f5362, 0xe07764b1,
            0x84ae6bbb, 0x1ca081fe, 0x942b08f9, 0x58684870, 0x19fd458f,
            0x876cde94, 0xb7f87b52, 0x23d373ab, 0xe2024b72, 0x578f1fe3,
            0x2aab5566, 0x0728ebb2, 0x03c2b52f, 0x9a7bc586, 0xa50837d3,
            0xf2872830, 0xb2a5bf23, 0xba6a0302, 0x5c8216ed, 0x2b1ccf8a,
            0x92b479a7, 0xf0f207f3, 0xa1e2694e, 0xcdf4da65, 0xd5be0506,
            0x1f6234d1, 0x8afea6c4, 0x9d532e34, 0xa055f3a2, 0x32e18a05,
            0x75ebf6a4, 0x39ec830b, 0xaaef6040, 0x069f715e, 0x51106ebd,
            0xf98a213e, 0x3d06dd96, 0xae053edd, 0x46bde64d, 0xb58d5491,
            0x055dc471, 0x6fd40604, 0xff155060, 0x24fb9819, 0x97e9bdd6,
            0xcc434089, 0x779ed967, 0xbd42e8b0, 0x888b8907, 0x385b19e7,
            0xdbeec879, 0x470a7ca1, 0xe90f427c, 0xc91e84f8, 0x00000000,
            0x83868009, 0x48ed2b32, 0xac70111e, 0x4e725a6c, 0xfbff0efd,
            0x5638850f, 0x1ed5ae3d, 0x27392d36, 0x64d90f0a, 0x21a65c68,
            0xd1545b9b, 0x3a2e3624, 0xb1670a0c, 0x0fe75793, 0xd296eeb4,
            0x9e919b1b, 0x4fc5c080, 0xa220dc61, 0x694b775a, 0x161a121c,
            0x0aba93e2, 0xe52aa0c0, 0x43e0223c, 0x1d171b12, 0x0b0d090e,
            0xadc78bf2, 0xb9a8b62d, 0xc8a91e14, 0x8519f157, 0x4c0775af,
            0xbbdd99ee, 0xfd607fa3, 0x9f2601f7, 0xbcf5725c, 0xc53b6644,
            0x347efb5b, 0x7629438b, 0xdcc623cb, 0x68fcedb6, 0x63f1e4b8,
            0xcadc31d7, 0x10856342, 0x40229713, 0x2011c684, 0x7d244a85,
            0xf83dbbd2, 0x1132f9ae, 0x6da129c7, 0x4b2f9e1d, 0xf330b2dc,
            0xec52860d, 0xd0e3c177, 0x6c16b32b, 0x99b970a9, 0xfa489411,
            0x2264e947, 0xc48cfca8, 0x1a3ff0a0, 0xd82c7d56, 0xef903322,
            0xc74e4987, 0xc1d138d9, 0xfea2ca8c, 0x360bd498, 0xcf81f5a6,
            0x28de7aa5, 0x268eb7da, 0xa4bfad3f, 0xe49d3a2c, 0x0d927850,
            0x9bcc5f6a, 0x62467e54, 0xc2138df6, 0xe8b8d890, 0x5ef7392e,
            0xf5afc382, 0xbe805d9f, 0x7c93d069, 0xa92dd56f, 0xb31225cf,
            0x3b99acc8, 0xa77d1810, 0x6e639ce8, 0x7bbb3bdb, 0x097826cd,
            0xf418596e, 0x01b79aec, 0xa89a4f83, 0x656e95e6, 0x7ee6ffaa,
            0x08cfbc21, 0xe6e815ef, 0xd99be7ba, 0xce366f4a, 0xd4099fea,
            0xd67cb029, 0xafb2a431, 0x31233f2a, 0x3094a5c6, 0xc066a235,
            0x37bc4e74, 0xa6ca82fc, 0xb0d090e0, 0x15d8a733, 0x4a9804f1,
            0xf7daec41, 0x0e50cd7f, 0x2ff69117, 0x8dd64d76, 0x4db0ef43,
            0x544daacc, 0xdf0496e4, 0xe3b5d19e, 0x1b886a4c, 0xb81f2cc1,
            0x7f516546, 0x04ea5e9d, 0x5d358c01, 0x737487fa, 0x2e410bfb,
            0x5a1d67b3, 0x52d2db92, 0x335610e9, 0x1347d66d, 0x8c61d79a,
            0x7a0ca137, 0x8e14f859, 0x893c13eb, 0xee27a9ce, 0x35c961b7,
            0xede51ce1, 0x3cb1477a, 0x59dfd29c, 0x3f73f255, 0x79ce1418,
            0xbf37c773, 0xeacdf753, 0x5baafd5f, 0x146f3ddf, 0x86db4478,
            0x81f3afca, 0x3ec468b9, 0x2c342438, 0x5f40a3c2, 0x72c31d16,
            0x0c25e2bc, 0x8b493c28, 0x41950dff, 0x7101a839, 0xdeb30c08,
            0x9ce4b4d8, 0x90c15664, 0x6184cb7b, 0x70b632d5, 0x745c6c48,
            0x4257b8d0
        };

        private uint C0, C1, C2, C3;
        private int ROUNDS;
        private uint[,] WorkingKey;
        private bool forEncryption;

        public string AlgorithmName => "AES";
        public bool IsPartialBlockOkay => false;
        public static int GetBlockSize() => BLOCK_SIZE;

        private uint Shift(uint r, int shift) => (r >> shift) | (r << (0x20 - shift));

        private uint FFmulX(uint x) => ((x & M2) << 0x1) ^ (((x & M1) >> 0x7) * M3);

        /**
        * The following defines provide alternative definitions of FFmulX that might
        * give improved performance if a fast 32-bit multiply is not available.
        *
        * private int FFmulX(int x) { int u = x & m1; u |= (u >> 1); return ((x & m2) << 1) ^ ((u >>> 3) | (u >>> 6)); }
        * private static final int  m4 = 0x1b1b1b1b;
        * private int FFmulX(int x) { int u = x & m1; return ((x & m2) << 1) ^ ((u - (u >>> 7)) & m4); }
        */

        private uint Inv_Mcol(uint x)
        {
            var f2 = FFmulX(x);
            var f4 = FFmulX(f2);
            var f8 = FFmulX(f4);
            var f9 = x ^ f8;

            return f2 ^ f4 ^ f8 ^ Shift(f2 ^ f9, 0x8) ^ Shift(f4 ^ f9, 0x10) ^ Shift(f9, 0x18);
        }

        private uint SubWord(uint x)
        {
            return S[x & 0xFF]
                   | ((uint)S[(x >> 0x8) & 0xFF] << 0x8)
                   | ((uint)S[(x >> 0x10) & 0xFF] << 0x10)
                   | ((uint)S[(x >> 0x18) & 0xFF] << 0x18);
        }

        /**
        * Calculate the necessary round keys
        * The number of calculations depends on key size and block size
        * AES specified a fixed block size of 128 bits and key sizes 128/192/256 bits
        * This code is written assuming those are the only possible values
        */

        private uint[,] GenerateWorkingKey(byte[] key, bool forEncryption)
        {
            var KC = key.Length / 0x4; // key length in words
            var t = 0x0;

            if ((KC != 0x4) && (KC != 0x6) && (KC != 0x8)) throw new ArgumentException("Key length not 128/192/256 bits.");

            ROUNDS = KC + 0x6; // This is not always true for the generalized Rijndael that allows larger block sizes
            var W = new uint[ROUNDS + 0x1, 0x4]; // 4 words in a block

            //
            // copy the key into the round key array
            //

            for (var i = 0x0; i < key.Length; t++)
            {
                W[t >> 0x2, t & 0x3] = key.LE_To_UInt32(i);
                i += 0x4;
            }

            //
            // while not enough round key material calculated
            // calculate new values
            //
            var k = (ROUNDS + 0x1) << 0x2;
            for (var i = KC; (i < k); i++)
            {
                var temp = W[(i - 0x1) >> 0x2, (i - 0x1) & 0x3];
                if ((i % KC) == 0x0)
                {
                    temp = SubWord(Shift(temp, 0x8)) ^ Rcon[(i / KC) - 0x1];
                }
                else if ((KC > 0x6) && ((i % KC) == 0x4))
                {
                    temp = SubWord(temp);
                }

                W[i >> 0x2, i & 0x3] = W[(i - KC) >> 0x2, (i - KC) & 0x3] ^ temp;
            }

            if (!forEncryption)
            {
                for (var j = 0x1; j < ROUNDS; j++)
                {
                    for (var i = 0x0; i < 0x4; i++)
                    {
                        W[j, i] = Inv_Mcol(W[j, i]);
                    }
                }
            }

            return W;
        }

        public void Init(bool forEncryption, byte[] key)
        {
            WorkingKey = GenerateWorkingKey(key, forEncryption);
            this.forEncryption = forEncryption;
        }

        public int ProcessBlock(byte[] input, int inOff, byte[] output, int outOff)
        {
            if (WorkingKey == null) throw new InvalidOperationException("AES engine not initialised");
            if ((inOff + (0x20 / 0x2)) > input.Length) throw new InvalidOperationException("input buffer too short");
            if ((outOff + (0x20 / 0x2)) > output.Length) throw new InvalidOperationException("output buffer too short");

            UnPackBlock(input, inOff);

            if (forEncryption)
            {
                EncryptBlock(WorkingKey);
            }
            else
            {
                DecryptBlock(WorkingKey);
            }

            PackBlock(output, outOff);

            return BLOCK_SIZE;
        }

        private void UnPackBlock(byte[] bytes, int off)
        {
            C0 = bytes.LE_To_UInt32(off);
            C1 = bytes.LE_To_UInt32(off + 0x4);
            C2 = bytes.LE_To_UInt32(off + 0x8);
            C3 = bytes.LE_To_UInt32(off + 0xC);
        }

        private void PackBlock(byte[] bytes, int off)
        {
            C0.UInt32_To_LE(bytes, off);
            C1.UInt32_To_LE(bytes, off + 0x4);
            C2.UInt32_To_LE(bytes, off + 0x8);
            C3.UInt32_To_LE(bytes, off + 0xC);
        }

        private void EncryptBlock(uint[,] KW)
        {
            uint r, r0, r1, r2, r3;

            C0 ^= KW[0x0, 0x0];
            C1 ^= KW[0x0, 0x1];
            C2 ^= KW[0x0, 0x2];
            C3 ^= KW[0x0, 0x3];

            for (r = 0x1; r < ROUNDS - 0x1;)
            {
                r0 = T0[C0 & 0xFF] ^ Shift(T0[(C1 >> 0x8) & 0xFF], 0x18) ^ Shift(T0[(C2 >> 0x10) & 0xFF], 0x10) ^
                     Shift(T0[(C3 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x0];
                r1 = T0[C1 & 0xFF] ^ Shift(T0[(C2 >> 0x8) & 0xFF], 0x18) ^ Shift(T0[(C3 >> 0x10) & 0xFF], 0x10) ^
                     Shift(T0[(C0 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x1];
                r2 = T0[C2 & 0xFF] ^ Shift(T0[(C3 >> 0x8) & 0xFF], 0x18) ^ Shift(T0[(C0 >> 0x10) & 0xFF], 0x10) ^
                     Shift(T0[(C1 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x2];
                r3 = T0[C3 & 0xFF] ^ Shift(T0[(C0 >> 0x8) & 0xFF], 0x18) ^ Shift(T0[(C1 >> 0x10) & 0xFF], 0x10) ^
                     Shift(T0[(C2 >> 0x18) & 0xFF], 0x8) ^ KW[r++, 0x3];
                C0 = T0[r0 & 0xFF] ^ Shift(T0[(r1 >> 0x8) & 0xFF], 0x18) ^ Shift(T0[(r2 >> 0x10) & 0xFF], 0x10) ^
                     Shift(T0[(r3 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x0];
                C1 = T0[r1 & 0xFF] ^ Shift(T0[(r2 >> 0x8) & 0xFF], 0x18) ^ Shift(T0[(r3 >> 0x10) & 0xFF], 0x10) ^
                     Shift(T0[(r0 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x1];
                C2 = T0[r2 & 0xFF] ^ Shift(T0[(r3 >> 0x8) & 0xFF], 0x18) ^ Shift(T0[(r0 >> 0x10) & 0xFF], 0x10) ^
                     Shift(T0[(r1 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x2];
                C3 = T0[r3 & 0xFF] ^ Shift(T0[(r0 >> 0x8) & 0xFF], 0x18) ^ Shift(T0[(r1 >> 0x10) & 0xFF], 0x10) ^
                     Shift(T0[(r2 >> 0x18) & 0xFF], 0x8) ^ KW[r++, 0x3];
            }

            r0 = T0[C0 & 0xFF] ^ Shift(T0[(C1 >> 0x8) & 0xFF], 0x18) ^ Shift(T0[(C2 >> 0x10) & 0xFF], 0x10) ^
                 Shift(T0[(C3 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x0];
            r1 = T0[C1 & 0xFF] ^ Shift(T0[(C2 >> 0x8) & 0xFF], 0x18) ^ Shift(T0[(C3 >> 0x10) & 0xFF], 0x10) ^
                 Shift(T0[(C0 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x1];
            r2 = T0[C2 & 0xFF] ^ Shift(T0[(C3 >> 0x8) & 0xFF], 0x18) ^ Shift(T0[(C0 >> 0x10) & 0xFF], 0x10) ^
                 Shift(T0[(C1 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x2];
            r3 = T0[C3 & 0xFF] ^ Shift(T0[(C0 >> 0x8) & 0xFF], 0x18) ^ Shift(T0[(C1 >> 0x10) & 0xFF], 0x10) ^
                 Shift(T0[(C2 >> 0x18) & 0xFF], 0x8) ^ KW[r++, 0x3];

            // the final round's table is a simple function of S so we don't use a whole other four tables for it

            C0 = S[r0 & 0xFF] ^ (((uint)S[(r1 >> 0x8) & 0xFF]) << 0x8) ^ (((uint)S[(r2 >> 0x10) & 0xFF]) << 0x10) ^
                 (((uint)S[(r3 >> 0x18) & 0xFF]) << 0x18) ^ KW[r, 0x0];
            C1 = S[r1 & 0xFF] ^ (((uint)S[(r2 >> 0x8) & 0xFF]) << 0x8) ^ (((uint)S[(r3 >> 0x10) & 0xFF]) << 0x10) ^
                 (((uint)S[(r0 >> 0x18) & 0xFF]) << 0x18) ^ KW[r, 0x1];
            C2 = S[r2 & 0xFF] ^ (((uint)S[(r3 >> 0x8) & 0xFF]) << 0x8) ^ (((uint)S[(r0 >> 0x10) & 0xFF]) << 0x10) ^
                 (((uint)S[(r1 >> 0x18) & 0xFF]) << 0x18) ^ KW[r, 0x2];
            C3 = S[r3 & 0xFF] ^ (((uint)S[(r0 >> 0x8) & 0xFF]) << 0x8) ^ (((uint)S[(r1 >> 0x10) & 0xFF]) << 0x10) ^
                 (((uint)S[(r2 >> 0x18) & 0xFF]) << 0x18) ^ KW[r, 0x3];
        }

        private void DecryptBlock(uint[,] KW)
        {
            int r;
            uint r0, r1, r2, r3;

            C0 ^= KW[ROUNDS, 0x0];
            C1 ^= KW[ROUNDS, 0x1];
            C2 ^= KW[ROUNDS, 0x2];
            C3 ^= KW[ROUNDS, 0x3];

            for (r = ROUNDS - 0x1; r > 0x1;)
            {
                r0 = Tinv0[C0 & 0xFF] ^ Shift(Tinv0[(C3 >> 0x8) & 0xFF], 0x18) ^ Shift(Tinv0[(C2 >> 0x10) & 0xFF], 0x10) ^
                     Shift(Tinv0[(C1 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x0];
                r1 = Tinv0[C1 & 0xFF] ^ Shift(Tinv0[(C0 >> 0x8) & 0xFF], 0x18) ^ Shift(Tinv0[(C3 >> 0x10) & 0xFF], 0x10) ^
                     Shift(Tinv0[(C2 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x1];
                r2 = Tinv0[C2 & 0xFF] ^ Shift(Tinv0[(C1 >> 0x8) & 0xFF], 0x18) ^ Shift(Tinv0[(C0 >> 0x10) & 0xFF], 0x10) ^
                     Shift(Tinv0[(C3 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x2];
                r3 = Tinv0[C3 & 0xFF] ^ Shift(Tinv0[(C2 >> 0x8) & 0xFF], 0x18) ^ Shift(Tinv0[(C1 >> 0x10) & 0xFF], 0x10) ^
                     Shift(Tinv0[(C0 >> 0x18) & 0xFF], 0x8) ^ KW[r--, 0x3];
                C0 = Tinv0[r0 & 0xFF] ^ Shift(Tinv0[(r3 >> 0x8) & 0xFF], 0x18) ^ Shift(Tinv0[(r2 >> 0x10) & 0xFF], 0x10) ^
                     Shift(Tinv0[(r1 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x0];
                C1 = Tinv0[r1 & 0xFF] ^ Shift(Tinv0[(r0 >> 0x8) & 0xFF], 0x18) ^ Shift(Tinv0[(r3 >> 0x10) & 0xFF], 0x10) ^
                     Shift(Tinv0[(r2 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x1];
                C2 = Tinv0[r2 & 0xFF] ^ Shift(Tinv0[(r1 >> 0x8) & 0xFF], 0x18) ^ Shift(Tinv0[(r0 >> 0x10) & 0xFF], 0x10) ^
                     Shift(Tinv0[(r3 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x2];
                C3 = Tinv0[r3 & 0xFF] ^ Shift(Tinv0[(r2 >> 0x8) & 0xFF], 0x18) ^ Shift(Tinv0[(r1 >> 0x10) & 0xFF], 0x10) ^
                     Shift(Tinv0[(r0 >> 0x18) & 0xFF], 0x8) ^ KW[r--, 0x3];
            }

            r0 = Tinv0[C0 & 0xFF] ^ Shift(Tinv0[(C3 >> 0x8) & 0xFF], 0x18) ^ Shift(Tinv0[(C2 >> 0x10) & 0xFF], 0x10) ^
                 Shift(Tinv0[(C1 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x0];
            r1 = Tinv0[C1 & 0xFF] ^ Shift(Tinv0[(C0 >> 0x8) & 0xFF], 0x18) ^ Shift(Tinv0[(C3 >> 0x10) & 0xFF], 0x10) ^
                 Shift(Tinv0[(C2 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x1];
            r2 = Tinv0[C2 & 0xFF] ^ Shift(Tinv0[(C1 >> 0x8) & 0xFF], 0x18) ^ Shift(Tinv0[(C0 >> 0x10) & 0xFF], 0x10) ^
                 Shift(Tinv0[(C3 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x2];
            r3 = Tinv0[C3 & 0xFF] ^ Shift(Tinv0[(C2 >> 0x8) & 0xFF], 0x18) ^ Shift(Tinv0[(C1 >> 0x10) & 0xFF], 0x10) ^
                 Shift(Tinv0[(C0 >> 0x18) & 0xFF], 0x8) ^ KW[r, 0x3];

            // the final round's table is a simple function of Si so we don't use a whole other four tables for it

            C0 = Si[r0 & 0xFF] ^ (((uint)Si[(r3 >> 0x8) & 0xFF]) << 0x8) ^ (((uint)Si[(r2 >> 0x10) & 0xFF]) << 0x10) ^
                 (((uint)Si[(r1 >> 0x18) & 0xFF]) << 0x18) ^ KW[0x0, 0x0];
            C1 = Si[r1 & 0xFF] ^ (((uint)Si[(r0 >> 0x8) & 0xFF]) << 0x8) ^ (((uint)Si[(r3 >> 0x10) & 0xFF]) << 0x10) ^
                 (((uint)Si[(r2 >> 0x18) & 0xFF]) << 0x18) ^ KW[0x0, 0x1];
            C2 = Si[r2 & 0xFF] ^ (((uint)Si[(r1 >> 0x8) & 0xFF]) << 0x8) ^ (((uint)Si[(r0 >> 0x10) & 0xFF]) << 0x10) ^
                 (((uint)Si[(r3 >> 0x18) & 0xFF]) << 0x18) ^ KW[0x0, 0x2];
            C3 = Si[r3 & 0xFF] ^ (((uint)Si[(r2 >> 0x8) & 0xFF]) << 0x8) ^ (((uint)Si[(r1 >> 0x10) & 0xFF]) << 0x10) ^
                 (((uint)Si[(r0 >> 0x18) & 0xFF]) << 0x18) ^ KW[0x0, 0x3];
        }
    }
}