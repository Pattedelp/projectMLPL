using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TorneoAmigos.Data;

namespace TorneoAmigos.Filters
{
    public class PrimeraCFilter : IActionFilter
    {
        private readonly TemporadaRepository _tempRepo;
        public PrimeraCFilter(TemporadaRepository tempRepo) => _tempRepo = tempRepo;

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.Controller is Controller ctrl)
                ctrl.ViewBag.PrimeraCActiva = _tempRepo.PrimeraCActiva();
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
