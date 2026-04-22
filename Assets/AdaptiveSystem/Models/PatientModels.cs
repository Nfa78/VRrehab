using System;

namespace AdaptiveSystem.Models
{
    [Serializable]
    public class PatientUpsertRequest
    {
        public string patient_code;
        public string dominant_hand;
        public string notes;
    }

    [Serializable]
    public class PatientPatchRequest
    {
        public string patient_code;
        public string dominant_hand;
        public string notes;
    }

    [Serializable]
    public class PatientProfile
    {
        public string patient_code;
        public string dominant_hand;
        public string notes;
    }
}
