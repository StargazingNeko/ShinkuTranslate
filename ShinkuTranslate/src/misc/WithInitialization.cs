﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ShinkuTranslate.misc {
    abstract class WithInitialization {
        private readonly TimeSpan MAX_WAIT_TIME = TimeSpan.FromSeconds(10);

        protected enum State {
            IDLE,
            INITIALIZING,
            WORKING,
            ERROR
        }

        protected volatile State state = State.IDLE;
        protected ManualResetEventSlim initialized = new ManualResetEventSlim();

        protected void tryWaitForInitialization() {
            if (state == State.INITIALIZING) {
                initialized.Wait(MAX_WAIT_TIME);
            }
        }

        public void initialize() {
            if (state == State.IDLE) {
                lock (this) {
                    if (state == State.IDLE) {
                        try {
                            state = State.INITIALIZING;
                            doInitialize();
                            state = State.WORKING;
                        } catch (Exception ex) {
                            onInitializationError(ex);
                            state = State.ERROR;
                        }
                        initialized.Set();
                    }
                }
            }
        }

        public void deinitialize() {
            tryWaitForInitialization();
            lock (this) {
                state = State.IDLE;
                initialized.Reset();
            }
        }

        protected abstract void doInitialize();
        protected abstract void onInitializationError(Exception ex);
    }
}
