using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapPartyAssist.Types {
    public class CheckpointResults {
        public Checkpoint Checkpoint { get; init; }
        public DateTime Time { get; private set; }
        public bool IsReached {
            get { return _isReached; }
            set {
                if(value && !_isReached) {
                    Time = DateTime.Now;
                }
                _isReached = value;
            }
        }

        private bool _isReached = false;

        public CheckpointResults(Checkpoint checkpoint, bool isReached = false) {
            Checkpoint = checkpoint;
            IsReached = isReached;
        }
    }
}
