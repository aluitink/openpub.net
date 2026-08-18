// The WebFinger endpoint for the WebUI is provided by the Core
// ActivityPub API controller (ActivityPub.Core.API.Controllers.WellKnown.
// WebFingerController), which is auto-discovered by MapControllers() because
// the WebUI references the Core assembly. A second controller mapped to the
// same /.well-known/webfinger route would cause an AmbiguousMatchException,
// so this file intentionally defines no controller. The Core controller's
// `self` link points at /users/{username}, which is served (with request-
// host-correct URLs) by the Core ActorController.
namespace ActivityPub.WebUI.Controllers;
