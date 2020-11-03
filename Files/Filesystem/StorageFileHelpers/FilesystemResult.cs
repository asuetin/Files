using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Files.Filesystem
{
    public enum FilesystemErrorCode
    {
        ERROR_GENERIC = -1,
        ERROR_OK = 0,
        ERROR_UNAUTHORIZED,
        ERROR_NOTFOUND,
        ERROR_INUSE,
        ERROR_NAMETOOLONG,
        ERROR_NOTAFOLDER,
        ERROR_NOTAFILE
    }

    public class FilesystemResult<T> : FilesystemResult
    {
        public FilesystemResult(T result, FilesystemErrorCode errorCode) : base(errorCode)
        {
            this.Result = result;
        }

        public T Result { get; private set; }

        public static implicit operator T(FilesystemResult<T> res) => res.Result;
    }

    public class FilesystemResult
    {
        public FilesystemResult(FilesystemErrorCode errorCode)
        {
            this.ErrorCode = errorCode;
        }

        public FilesystemErrorCode ErrorCode { get; private set; }

        public static implicit operator FilesystemErrorCode(FilesystemResult res) => res.ErrorCode;

        public static implicit operator bool(FilesystemResult res) => 
            res.ErrorCode == FilesystemErrorCode.ERROR_OK;
        public static explicit operator FilesystemResult(bool res) =>
            new FilesystemResult(res ? FilesystemErrorCode.ERROR_OK : FilesystemErrorCode.ERROR_GENERIC);
    }

    public static class TaskExtensions
    {
        private static FilesystemErrorCode GetErrorCode(Exception ex)
        {
            if (ex is UnauthorizedAccessException)
            {
                return FilesystemErrorCode.ERROR_UNAUTHORIZED;
            }
            else if (ex is FileNotFoundException // Item was deleted
                || ex is System.Runtime.InteropServices.COMException // Item's drive was ejected
                || (uint)ex.HResult == 0x8007000F) // The system cannot find the drive specified
            {
                return FilesystemErrorCode.ERROR_NOTFOUND;
            }
            else if (ex is IOException || ex is FileLoadException)
            {
                return FilesystemErrorCode.ERROR_INUSE;
            }
            else if (ex is PathTooLongException)
            {
                return FilesystemErrorCode.ERROR_NAMETOOLONG;
            }
            else if (ex is ArgumentException) // Item was invalid
            {
                return FilesystemErrorCode.ERROR_NOTAFOLDER;
            }
            else if ((uint)ex.HResult == 0x800700A1 // The specified path is invalid (usually an mtp device was disconnected)
                || (uint)ex.HResult == 0x8007016A // The cloud file provider is not running
                || (uint)ex.HResult == 0x8000000A) // The data necessary to complete this operation is not yet available)
            {
                return FilesystemErrorCode.ERROR_GENERIC;
            }
            else
            {
                return FilesystemErrorCode.ERROR_GENERIC;
            }
        }

        public async static Task<FilesystemResult<T>> Wrap<T>(this Task<T> wrapped)
        {
            try
            {
                return new FilesystemResult<T>(await wrapped, FilesystemErrorCode.ERROR_OK);
            }
            catch (Exception ex)
            {
                return new FilesystemResult<T>(default(T), GetErrorCode(ex));
            }
        }

        public async static Task<FilesystemResult> Wrap(this Task wrapped)
        {
            try
            {
                await wrapped;
                return new FilesystemResult(FilesystemErrorCode.ERROR_OK);
            }
            catch (Exception ex)
            {
                return new FilesystemResult(GetErrorCode(ex));
            }
        }
    }
}
