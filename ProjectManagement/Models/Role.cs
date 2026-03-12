using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WpfResourceGantt.ProjectManagement.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Role
    {
        Administrator,
        FlightChief,
        SectionChief,
        TechnicalSpecialist,
        ProjectManager,
        Developer,
        ConfigurationManager,
        Technician,
        ElectricalDesignEngineer,
        MechanicalDesignEngineer,
        TechWriter
    }
}
