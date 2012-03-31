using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;

using Codeology.WinAPI;

namespace Codeology.IPC.Mailslots
{

    public class MailslotClient : IDisposable
    {

        private bool disposed;
        private string name;
        private IntPtr handle;

        public MailslotClient(string mailslotName) : this(mailslotName,String.Empty)
        {
            // Do nothing...
        }

        public MailslotClient(string mailslotName, string machineName)
        {
            disposed = false;
            name = String.Format(@"\\{1}\mailslot\{0}",mailslotName,(String.IsNullOrEmpty(machineName) ? "." : String.Empty));
            handle = IntPtr.Zero;
        }

        #region Methods

        public void Dispose()
        {
            if (!disposed) {
                // Disconnect mailslot (if connected)
                Disconnect();

                // Mark as disposed
                disposed = true;
            }
        }

        public void Connect()
        {
            // Create mailslot
            handle = Kernel.CreateFile(name,Kernel.FileAccess.GenericWrite,Kernel.FileShare.Read,IntPtr.Zero,Kernel.FileMode.OpenExisting,Kernel.FileAttributes.None,IntPtr.Zero);

            if (handle == IntPtr.Zero) throw new MailslotException("Could not connect to mailslot.");
        }

        public void Disconnect()
        {
            // If no handle, return
            if (handle == IntPtr.Zero) return;

            // Close mailslot
            Kernel.CloseHandle(handle);

            // Zero handle
            handle = IntPtr.Zero;
        }

        public int Write(byte[] buffer)
        {
            return Write(buffer,0,buffer.Length);
        }

        public int Write(byte[] buffer, int offset, int count)
        {
            // Allocate new buffer
            byte[] sub_buffer = new byte[count];

            // Copy data from buffer to new buffer
            Array.Copy(buffer,offset,sub_buffer,0,count);

            // Write out new buffer to mailslot
            uint bytes_wrote = 0;
            NativeOverlapped overlapped = new NativeOverlapped();

            if (!Kernel.WriteFile(handle,sub_buffer,(uint)sub_buffer.Length,out bytes_wrote,ref overlapped)) throw new MailslotException("Could not write to mailslot.");

            // Return
            return (int)bytes_wrote;
        }

        public IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback asyncCallback, object userState)
        {
            return new WriteAsyncResult(handle,buffer,offset,count,asyncCallback,userState);
        }

        public int EndWrite(IAsyncResult asyncResult)
        {
            // Get result object from async result
            WriteAsyncResult result = (WriteAsyncResult)asyncResult;

            // Return
            return (int)result.BytesWritten;
        }

        #endregion

        #region Properties

        public string Name
        {
            get {
                return name;
            }
        }

        public IntPtr Handle
        {
            get {
                return handle;
            }
        }

        #endregion

    }

    internal class WriteAsyncResult : IAsyncResult
    {

        private object locker;
        private IntPtr handle;
        private AsyncCallback callback;
        private object state;
        private ManualResetEvent mre;
        private bool is_complete;
        private int bytes_wrote;

        internal WriteAsyncResult(IntPtr mailslotHandle, byte[] buffer, int offset, int count, AsyncCallback asyncCallback, object asyncState)
        {
            locker = new object();
            handle = mailslotHandle;
            callback = asyncCallback;
            state = asyncState;
            mre = null;
            is_complete = false;
            bytes_wrote = 0;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Allocate buffer
                byte[] buf = new byte[count];

                // Allocate new buffer
                byte[] sub_buffer = new byte[count];

                // Copy data from buffer to new buffer
                Array.Copy(buffer,offset,sub_buffer,0,count);

                // Write out new buffer to mailslot
                uint num_wrote = 0;
                NativeOverlapped overlapped = new NativeOverlapped();

                if (!Kernel.WriteFile(handle,buf,(uint)count,out num_wrote,ref overlapped)) throw new MailslotException("Could not write message to mailslot.");

                lock (locker) {
                    // Set bytes wrote
                    bytes_wrote = (int)num_wrote;

                    // Mark is complete
                    is_complete = true;
                }

                // Signal event
                ManualResetEvent reset_event;

                lock (locker) {
                    reset_event = mre;
                }

                if (reset_event != null) reset_event.Set();

                // Call callback
                if (callback != null) callback(this);
            }));
        }

        #region Methods

        #endregion

        #region Properties

        public object AsyncState
        {
            get {
                return state;
            }
        }

        public WaitHandle AsyncWaitHandle
        {
            get { 
                lock (locker) {
                    if (mre == null) mre = new ManualResetEvent(false);

                    return mre;
                }
            }
        }

        public bool CompletedSynchronously
        {
            get { 
                return false;
            }
        }

        public bool IsCompleted
        {
            get { 
                lock (locker) {
                    return is_complete;
                }
            }
        }

        internal int BytesWritten
        {
            get {
                lock (locker) {
                    return bytes_wrote;
                }
            }
        }

        #endregion

    }

}