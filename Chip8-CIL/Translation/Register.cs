using System.Diagnostics;
using System.Numerics;
using System;

namespace Chip8_CIL
{
    namespace Register
    {
        public enum Id : byte
        {
            V0Register     = 0,
            V1Register     = 1,
            V2Register     = 2,
            V3Register     = 3,
            V4Register     = 4,
            V5Register     = 5,
            V6Register     = 6,
            V7Register     = 7,
            V8Register     = 8,
            V9Register     = 9,
            VARegister     = 0xa,
            VBRegister     = 0xb,
            VCRegister     = 0xc,
            VDRegister     = 0xd,
            VERegister     = 0xe,
            VFRegister     = 0xf,
            VRegisterCount = 0x10,
            IRegister      = 0x10,
            SPRegister     = 0x11,
            RegisterCount  = 0x12,
        }

        // The ordering of this MUST match the Id enum
        public struct Context
        {
            public byte V0Register;
            public byte V1Register;
            public byte V2Register;
            public byte V3Register;
            public byte V4Register;
            public byte V5Register;
            public byte V6Register;
            public byte V7Register;
            public byte V8Register;
            public byte V9Register;
            public byte VARegister;
            public byte VBRegister;
            public byte VCRegister;
            public byte VDRegister;
            public byte VERegister;
            public byte VFRegister; // Used for flags
            public ushort IRegister;
            public byte SPRegister;

            public static Type GetStorageType(Id id)
            {
                return typeof(Context).GetFields()[(int)id].FieldType;
            }

            public void Print()
            {
                Logger.StartFunction("Context Print", Logger.Level.Trace);
                foreach (var field in GetType().GetFields())
                    Logger.LogTrace(field.Name + ": " + field.GetValue(this));

                Logger.StopFunction();
            }
        }

        public struct Set
        {
            private uint _data;

            private Set(uint data)
            {
                _data = data;
            }

            public static Set Full() => new(0xffffffff);


            public int PopCnt()
            {
                return BitOperations.PopCount(_data);
            }

            public void SetRegister(Id id)
            {
                Trace.Assert(id < Id.RegisterCount);

                _data |= (uint)(1 << (byte)id);
            }

            public bool GetRegister(Id id)
            {
                Trace.Assert(id < Id.RegisterCount);

                return (_data & (uint)(1 << (byte)id)) != 0;
            }

            public void SetVRegister(Id id)
            {
                Trace.Assert(id < Id.VRegisterCount);

                _data |= (uint)(1 << (byte)id);
            }

            public bool GetVRegister(Id id)
            {
                Trace.Assert(id < Id.VRegisterCount);

                return (_data & (uint)(1 << (byte)id)) != 0;
            }

            public void SetIRegister()
            {
                _data |= 1 << (byte)Id.IRegister;
            }

            public bool GetIRegister()
            {
                return (_data & (1 << (byte)Id.IRegister)) != 0;
            }

            public static Set operator &(in Set a, in Set b)
            {
                return new Set(a._data & b._data);
            }

            public static Set operator |(in Set a, in Set b)
            {
                return new Set(a._data | b._data);
            }

            public static Set operator ~(in Set a)
            {
                return new Set(~a._data);
            }

            public static bool operator ==(in Set a, in Set b)
            {
                return a._data == b._data;
            }

            public static bool operator !=(in Set a, in Set b)
            {
                return a._data != b._data;
            }

            public override bool Equals(object a)
            {
                return ((Set)a)._data == _data;
            }

            public override int GetHashCode()
            {
                return (int)_data;
            }

            public override string ToString() => string.Format("{0:X}", _data);
            
        }

        public struct UsageInfo
        {
            public Register.Set Changed; // Registers that have been overwritten but rely on their previous value
            public Register.Set Clobbered; // Registers that have been overwritten and don't rely on their previous value
            public Register.Set Read; // Registers that have been read
        }
    }
}
