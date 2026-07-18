namespace AppiumSetupManager.Core.Models;

/// <summary>
/// Tri-state deletion risk for a <see cref="StorageItem"/>. <see cref="Low"/> items are pure
/// caches that tools rebuild on demand; <see cref="Review"/> items are user assets that look
/// stale (e.g. an AVD untouched for 30+ days); <see cref="InUse"/> items appear actively used
/// and should not be pre-selected for cleanup.
/// </summary>
public enum RiskLevel { Low, Review, InUse }
