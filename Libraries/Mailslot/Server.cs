using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;

using Codeology.WinAPI;

namespace Codeology.IPC.Mailslots
{

    public class MailslotServer : IDisposable
    {

        private bool disposed;
        private string name;
        private IntPtr handle;

        public MailslotServer(string mailslotName) : this(mailslotName,String.Empty)
        {
            // Do nothing...
        }

        public MailslotServer(string mailslotName, string machineName)
        {
            disposed = false;
            name = String.Format(@"\\{1}\mailslot\{0}",mailslotName,(String.IsNullOrEmpty(machineName) ? "." : machineName));
            handle = IntPtr.Zero;
        }

        #region Methods

        public void Dispose()
        {
            if (!disposed) {
                // Close mailslot (if open)
                Close();

                // Mark as disposed
                disposed = true;
            }
        }

        public void Open()
        {
            // Create mailslot
            handle = Kernel.CreateMailslot(name,0,0,IntPtr.Zero);

            if (handle == IntPtr.Zero) throw new MailslotException("Could not create mailslot.");
        }

        public void Close()
        {
            // If no handle, return
            if (handle == IntPtr.Zero) return;

            // Close mailslot
            Kernel.CloseHandle(handle);

            // Zero handle
            handle = IntPtr.Zero;
        }

        public void Wait()
        {
            // Initialize message size
            uint msg_count = 0;
            uint msg_size = 0;

            while (true) {
                // Get mailslot info
                if (!Kernel.GetMailslotInfo(handle,0,ref msg_size,ref msg_count,IntPtr.Zero)) throw new MailslotException("Failed to get message information from mailslot.");

                // If we have a message, stop waiting
                if (msg_count > 0 && msg_size > 0) break;

                // Sleep for a bit
                Thread.Sleep(10);
            }
        }

        public IAsyncResult BeginWait(AsyncCallback asyncCallback, object userState)
        {
            return new WaitAsyncResult(handle,asyncCallback,userState);
        }

        public int EndWait(IAsyncResult asyncResult)
        {
            // Get result object from async result
            WaitAsyncResult result = (WaitAsyncResult)asyncResult;

            // Return
            return result.MessageSize;
        }

        public void CancelWait(IAsyncResult asyncResult)
        {
            // Get result object from async result
            WaitAsyncResult result = (WaitAsyncResult)asyncResult;

            // Cancel result object
            result.Cancel();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            // Allocate buffer for message
            byte[] sub_buffer = new byte[count];

            // Read in message from mailslot
            uint num_read = 0;

            if (!Kernel.ReadFile(handle,sub_buffer,(uint)count,out num_read,IntPtr.Zero)) throw new MailslotException("Could not read message from mailslot.");

            // Copy buffers
            Array.Copy(sub_buffer,0,buffer,offset,num_read);

            // Return
            return (int)num_read;
        }

        public IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback asyncCallback, object userState)
        {
            return new ReadAsyncResult(handle,buffer,offset,count,asyncCallback,userState);
        }

        public int EndRead(IAsyncResult asyncResult)
        {
            // Get result object from async result
            ReadAsyncResult result = (ReadAsyncResult)asyncResult;

            // Return
            return (int)result.BytesRead;
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

    internal class WaitAsyncResult : IAsyncResult
    {

        private object locker;
        private IntPtr handle;
        private AsyncCallback callback;
        private object state;
        private ManualResetEvent mre;
        private bool is_complete;
        private uint message_size;
        private bool thread_cancel;

        internal WaitAsyncResult(IntPtr mailslotHandle, AsyncCallback asyncCallback, object asyncState)
        {
            locker = new object();
            handle = mailslotHandle;
            callback = asyncCallback;
            state = asyncState;
            mre = null;
            is_complete = false;
            message_size = 0;
            thread_cancel = false;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Initialize message count size
                uint msg_count = 0;
                uint msg_size = 0;

                while (true) {
                    lock (locker) {
                        // Check for cancellation
                        if (thread_cancel) return;
                    }

                    // Get mailslot info
                    if (!Kernel.GetMailslotInfo(handle,0,ref msg_size,ref msg_count,IntPtr.Zero)) throw new MailslotException("Failed to get message information from mailslot.");

                    // If we have a message, stop waiting
                    if (msg_count > 0 && msg_size > 0) break;

                    // Sleep for a bit
                    Thread.Sleep(10);
                }

                lock (locker) {
                    // Set message size
                    message_size = msg_size;

                    // Mark is complete
                    is_complete = true;
                }

                // Signal event
                if (mre != null) mre.Set();

                // Call callback
                if (callback != null) callback(this);
            }));
        }

        #region Methods

        internal void Cancel()
        {
            lock (locker) {
                thread_cancel = true;
            }
        }

        #endregion

        #region Properties

        public object AsyncState
        {
            get {
                lock (locker) {
                    return state;
                }
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

        internal int MessageSize
        {
            get {
                lock (locker) {
                    return (int)message_size;
                }
            }
        }

        #endregion

    }

    internal class ReadAsyncResult : IAsyncResult
    {

        private object locker;
        private IntPtr handle;
        private AsyncCallback callback;
        private object state;
        private ManualResetEvent mre;
        private bool is_complete;
        private int bytes_read;

        internal ReadAsyncResult(IntPtr mailslotHandle, byte[] buffer, int offset, int count, AsyncCallback asyncCallback, object asyncState)
        {
            locker = new object();
            handle = mailslotHandle;
            callback = asyncCallback;
            state = asyncState;
            mre = null;
            is_complete = false;
            bytes_read = 0;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Allocate buffer
                byte[] buf = new byte[count];

                // Read in message from mailslot into buffer
                uint num_read = 0;

                if (!Kernel.ReadFile(handle,buf,(uint)count,out num_read,IntPtr.Zero)) throw new MailslotException("Could not read message from mailslot.");

                lock (locker) {
                    // Set bytes read
                    bytes_read = (int)num_read;

                    // Copy buffers
                    Array.Copy(buf,0,buffer,offset,bytes_read);

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

        internal int BytesRead
        {
            get {
                lock (locker) {
                    return bytes_read;
                }
            }
        }

        #endregion

    }

}
