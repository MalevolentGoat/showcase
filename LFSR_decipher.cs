//efficient algortihm to decipher lfsr

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;

class task_1 {
    static void Main(string[] args) {
        LFSR lfsr = new LFSR();
        Stopwatch sw = new Stopwatch();
        sw.Start();
        using StreamReader sr = new StreamReader("Chiffretext.txt");
        string chiffre = "";
        while(!sr.EndOfStream) {
            uint buffer = 0x00;
            int i = 7;
            while(i>=0) {
                int read = sr.Read();
                if(read == 49) {
                    buffer |= 1u<<i;
                    i--;
                } else if(read == 48) {
                    i--;
                } else if(read == 93) {
                    break;
                }
            }
            chiffre += (char)buffer;
        }
        sr.Close();
        ulong seed = 0x00;
        List<ulong> seedList = new List<ulong>();
        do
        {
            lfsr.initialize(seed);
            lfsr.preShift();
            int weight = 0;
            for(int i=0; i<chiffre.Length; i++) {
                char encChar = lfsr.encipher(chiffre[i]);
                if((encChar > 64 && encChar < 91) || (encChar > 96 && encChar < 123)) {
                    weight++;
                }
            }
            if(weight > chiffre.Length/2) {
                seedList.Add(seed);
            }
            seed += 0b_0000_0000_0000_0000_0100_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000;
        } while (seed != 0x00);

        foreach(ulong entry in seedList) {
            lfsr.initialize(entry);
            lfsr.preShift();
            for(int i=0; i<chiffre.Length; i++) {
                Console.Write(lfsr.encipher(chiffre[i]));
            }
        }
        Console.WriteLine();
        sw.Stop();
        Console.WriteLine(sw.ElapsedMilliseconds/1000);// 13 Sekunden :D
    }
    //ask how to lfsr backwards
    class LFSR {
        //minimal feedback x^19 + x^18 + x^16 + x^15 + x^12 + x^11 + x^6 + x^5 + x^1 + 1
        public ulong lfsr = 0b_1010_1010_1010_1010_1010_1010_1010_1010_1010_1010_1010_1010_1010_1010_1010_0000;
        ulong fixedBit = 0b_1000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000;
        ulong mask_r2 = 0b_0010_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000;
        ulong mask_r6 =  0b_0000_0010_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000;
        ulong mask_r12 = 0b_0000_0000_0000_1000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000;
        ulong mask_r17 = 0b_0000_0000_0000_0000_0100_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000;
        public ulong mask_out = 0b_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0001_0000;
        
        public void preShift() {
            for(int i=0; i<512; i++) {
                shift();
            }
        }
        public void initialize(ulong init) {
            lfsr = init;
        }
        public void shift(int shiftBy = 1) {
            for(int i=0; i<shiftBy; i++) {
                ulong buffer = (fixedBit ^ (lfsr&mask_r2)<<2 ^ (lfsr&mask_r6)<<6 ^ (lfsr&mask_r12)<<12 ^ (lfsr&mask_r17)<<17);
                lfsr >>= 5;
                lfsr = (lfsr<<4) | buffer;
            }
        }
        public char encipher(char input) {
            byte stream = 0;
            for(int i=0; i<8; i++) {
                stream <<= 1;
                stream |= (byte)((lfsr & mask_out)>>4);
                shift();
            }
            byte outbyte = (byte)(input ^ stream);
            return (char)outbyte;
        }
    }
}