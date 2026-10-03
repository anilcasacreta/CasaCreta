// Grasshopper Script Instance
#region Usings
using System;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    #region Component documentation
    /*
    ============================================================================
    PatternSurfaceSelector
    ============================================================================

    PURPOSE
    -------
    This Grasshopper C# component detects which contour points touch surfaces from
    either explicit gradient set, applies one continuous repeating pattern across
    both hit and non-hit points, moves them with separate domains, and rebuilds one
    interpolated curve per original point-tree branch.

    The code deliberately keeps two different Boolean concepts:

      1. HitMask[i]
         True only when the original point at item i is within Tolerance of a
         surface from either input set.

      2. BooleanPattern[i] / KeptPatternPoints[i]
         True when item i belongs to the PatternCount / SpaceCount candidate
         sequence. This sequence continues through hit and non-hit areas without
         restarting at a surface boundary.

    A direct-hit candidate uses HitDomain; a non-hit candidate uses NotHitDomain.
    Do not replace the pattern mask with the raw hit mask when building final
    curves. Doing so merges adjacent surface hits into one large continuous shape
    and destroys the requested PatternCount / SpaceCount repetition.

    INPUTS
    ------
    Points
        DataTree<Point3d>. Each branch represents one ordered contour. Item order
        inside every branch is significant and is preserved in FinalPoints.

    LowToHigh
        Optional DataTree<Brep>. Direct-hit points move from HitDomain start at
        the bottom to HitDomain end at the top.

    HighToLow
        Optional DataTree<Brep>. Direct-hit points move from HitDomain end at the
        bottom to HitDomain start at the top.

        Both sets participate in the same hit and pattern logic. Either input may
        be null or empty. Internally, LowToHigh surfaces are flattened first and
        HighToLow surfaces second; this combined zero-based order defines every
        SurfaceIndices value.

    SwapInputSurfaces
        False uses the input meanings written above. True swaps only their gradient
        behavior: LowToHigh behaves HighToLow and HighToLow behaves LowToHigh.
        Surface indices and physical input connections do not change.

    NotHitExclusionSurfaces
        Optional DataTree<Brep>. If any non-hit point in one complete PatternCount
        block touches one of these surfaces, that entire non-hit pattern block is
        removed and its points remain at their original positions. These surfaces
        disable only NotHitDomain. Direct LowToHigh or HighToLow hits still use
        HitDomain because the previous hit-pattern rule has priority.

    Tolerance
        Maximum point-to-Brep distance accepted as a hit. Values <= 0 use the
        Rhino document absolute tolerance (or 0.001 if no document is available).

    PatternCount / SpaceCount
        Repeating item pattern. Example: PatternCount=2 and SpaceCount=2 produces
        two candidate pattern items followed by two spaces. Candidate items move;
        spaces remain unchanged. The sequence continues across both point groups.

    InvertPattern
        Shifts the repeating cycle so that the space portion comes first and the
        pattern portion comes second.

    MoveDirection
        Movement vector. Only its direction is used; the vector is normalized.
        Invalid or zero vectors safely fall back to World Y.

    HitDomain
        Grasshopper Domain decoded as lower/upper movement distances. It is applied
        independently to every surface for direct-hit candidate points.

    NotHitDomain
        Separate Domain for candidate points that do not directly hit a surface.
        Use a smaller domain here for the subtle/faded pattern outside the surfaces.
        This group has its own independent World-Z normalization range.

        The gradient coordinate is World Z because the input contour stack changes
        height along Z, even when actual movement is along Y (or another supplied
        direction).

    OUTPUTS
    -------
    HitMask
        Full-length Boolean tree aligned exactly with Points. Use this output when
        the original item index of a hit/non-hit point must be recovered.

    HitPoints / NotHitPoints
        Filtered point trees in original relative order. Because they are filtered,
        their local item numbers are compacted. HitMask is the authoritative
        full-index mapping.

    BooleanPattern
        Full-length pattern mask aligned exactly with Points. True means the item
        is moved and replaced in FinalPoints. All non-hit items in an excluded
        PatternCount block are false and remain unchanged.

    MovedPatternPoints
        All pattern points, after movement. This is filtered and aligned item-for-
        item with MovementDistances and SurfaceIndices. A SurfaceIndices value of
        -1 identifies a non-hit point moved with NotHitDomain.

    MovementDistances
        Movement amount for each item in MovedPatternPoints.

    SurfaceIndices
        Combined LowToHigh-then-HighToLow surface index for each moved direct-hit
        point. Non-hit pattern points have value -1.

    FinalPoints
        Full-length, index-aligned reconstruction tree. Every original item appears
        exactly once: a pattern item is replaced at the same index by
        its moved point; all other items remain at their original location.

    InterpolatedCurves
        One open interpolated curve per Points branch containing at least two valid
        ordered points. These curves are created from FinalPoints, never by joining
        filtered point outputs. If a contour has constant Z and moves along Y,
        Rhino may still call it a planar curve; its weaving is visible in Top view.

    PROCESSING PHASES
    -----------------
      A. Cache movement and exclusion Brep/face geometry.
      B. Evaluate raw hit and non-hit exclusion masks for every original point.
      C. Mark repeating blocks; remove a complete non-hit block if any item in it
         touches NotHitExclusionSurfaces.
      D. Find independent World-Z ranges per hit surface and for the non-hit set.
      E. Move pattern points, rebuild full ordered branches, and interpolate.

    PERFORMANCE AND LOOP SAFETY
    ---------------------------
    - Branch bounding boxes reject surfaces that cannot touch that contour.
    - Point and face bounding boxes reject expensive geometry tests early.
    - True PlaneSurface faces use direct plane parameters; other planar faces use
      the trim-aware BrepFace fallback; non-planar Breps use Brep.ClosestPoint.
    - Exact distances are retained for hit diagnostics and future extensions.
    - Every loop is bounded by an input branch, point, Brep, face, or pattern count;
      there is no unbounded or retry loop.
    ============================================================================
    */
    #endregion

	private void RunScript(
		DataTree<Point3d> Points,
		DataTree<Brep> LowToHigh,
		DataTree<Brep> HighToLow,
		bool SwapInputSurfaces,
		DataTree<Brep> NotHitExclusionSurfaces,
		double Tolerance,
		int PatternCount,
		int SpaceCount,
		bool InvertPattern,
		Vector3d MoveDirection,
		Interval HitDomain,
		Interval NotHitDomain,
		ref object HitMask,
		ref object HitPoints,
		ref object NotHitPoints,
		ref object BooleanPattern,
		ref object MovedPatternPoints,
		ref object MovementDistances,
		ref object SurfaceIndices,
		ref object FinalPoints,
		ref object InterpolatedCurves)
    {
        // Allocate only the output trees that are useful downstream. In particular,
        // FinalPoints and the two Boolean masks preserve full branch/item alignment;
        // filtered diagnostic point sets are kept separate from curve construction.
        DataTree<bool> hitMaskTree = new DataTree<bool>();
        DataTree<Point3d> hitPointsTree = new DataTree<Point3d>();
        DataTree<Point3d> notHitPointsTree = new DataTree<Point3d>();
        DataTree<bool> booleanPatternTree = new DataTree<bool>();
        DataTree<Point3d> movedPatternPointsTree = new DataTree<Point3d>();
        DataTree<double> movementDistancesTree = new DataTree<double>();
        DataTree<int> surfaceIndicesTree = new DataTree<int>();
        DataTree<Point3d> finalPointsTree = new DataTree<Point3d>();
        DataTree<Curve> interpolatedCurvesTree = new DataTree<Curve>();

        // ---------------------------------------------------------------------
        // Phase 0: sanitize scalar and vector inputs.
        // ---------------------------------------------------------------------
        double tolerance = Tolerance;

        if (tolerance <= 0.0)
        {
            tolerance = RhinoDocument != null ? RhinoDocument.ModelAbsoluteTolerance : 0.001;

            Component.AddRuntimeMessage(
                GH_RuntimeMessageLevel.Warning,
                "Tolerance was <= 0. Using document tolerance: " + tolerance.ToString());
        }

        int patternCount = PatternCount < 0 ? 0 : PatternCount;
        int spaceCount = SpaceCount < 0 ? 0 : SpaceCount;

        double hitMinimumDistance;
        double hitMaximumDistance;
        GetOrderedDomain(
            HitDomain,
            "HitDomain",
            out hitMinimumDistance,
            out hitMaximumDistance);

        double notHitMinimumDistance;
        double notHitMaximumDistance;
        GetOrderedDomain(
            NotHitDomain,
            "NotHitDomain",
            out notHitMinimumDistance,
            out notHitMaximumDistance);

        Vector3d moveDirection = MoveDirection;
        double moveDirectionLength = moveDirection.Length;
        bool hasMoveDirection =
            moveDirection.IsValid &&
            !double.IsNaN(moveDirectionLength) &&
            !double.IsInfinity(moveDirectionLength) &&
            moveDirectionLength > RhinoMath.ZeroTolerance;

        if (hasMoveDirection)
        {
            moveDirection /= moveDirectionLength;
        }

        // Movement direction and gradient direction are intentionally independent.
        // Points move along MoveDirection, but their distance is graded by height Z.
        Vector3d gradientAxis = Vector3d.ZAxis;

        if (!hasMoveDirection)
        {
            moveDirection = Vector3d.YAxis;
        }

        // ---------------------------------------------------------------------
        // Phase A: flatten and cache the movement and exclusion surfaces once.
        // ---------------------------------------------------------------------
        List<BrepRecord> breps = CollectBreps(
            LowToHigh,
            HighToLow,
            SwapInputSurfaces,
            tolerance);
        List<BrepRecord> notHitExclusionBreps = new List<BrepRecord>();
        AppendBreps(
            notHitExclusionBreps,
            NotHitExclusionSurfaces,
            false,
            tolerance);
        List<BranchEvaluation> evaluatedBranches = new List<BranchEvaluation>();

        if (breps.Count == 0)
        {
            Component.AddRuntimeMessage(
                GH_RuntimeMessageLevel.Warning,
                "No valid Breps found in LowToHigh or HighToLow. " +
                "Pattern candidates outside NotHitExclusionSurfaces will use NotHitDomain.");
        }

        // ---------------------------------------------------------------------
        // Phases B/C: raw hit evaluation followed by repeating-pattern activation.
        // BranchEvaluation stores arrays with the same length as the source branch.
        // ---------------------------------------------------------------------
        if (Points != null)
        {
            for (int branchIndex = 0; branchIndex < Points.BranchCount; branchIndex++)
            {
                GH_Path path = Points.Path(branchIndex);
                IList<Point3d> branchPoints = Points.Branch(branchIndex);

                if (branchPoints == null)
                {
                    continue;
                }

                BranchEvaluation branch = new BranchEvaluation(path, branchPoints);
                List<int> candidateSurfaceIndices = GetCandidateSurfaceIndices(
                    branchPoints,
                    breps,
                    tolerance);
                List<int> candidateExclusionIndices = GetCandidateSurfaceIndices(
                    branchPoints,
                    notHitExclusionBreps,
                    tolerance);

                for (int i = 0; i < branchPoints.Count; i++)
                {
                    Point3d point = branchPoints[i];

                    Point3d closestPoint;
                    double distance;
                    int surfaceIndex;

                    bool hit = TryGetClosestPointWithinTolerance(
                        point,
                        breps,
                        candidateSurfaceIndices,
                        tolerance,
                        out closestPoint,
                        out distance,
                        out surfaceIndex);

                    bool insideTolerance = hit;

                    branch.HitMask[i] = insideTolerance;
                    branch.Distances[i] = distance;
                    branch.SurfaceIndices[i] = insideTolerance ? surfaceIndex : -1;

                    // LowToHigh / HighToLow has priority. Exclusion is tested only
                    // for points that would otherwise receive NotHitDomain.
                    if (!insideTolerance &&
                        candidateExclusionIndices.Count > 0 &&
                        IsGreenPatternIndex(
                            i,
                            patternCount,
                            spaceCount,
                            InvertPattern))
                    {
                        Point3d exclusionClosestPoint;
                        double exclusionDistance;
                        int exclusionSurfaceIndex;

                        branch.NotHitExclusionMask[i] =
                            TryGetClosestPointWithinTolerance(
                                point,
                                notHitExclusionBreps,
                                candidateExclusionIndices,
                                tolerance,
                                out exclusionClosestPoint,
                                out exclusionDistance,
                                out exclusionSurfaceIndex);
                    }
                }

                // The candidate sequence uses original item indices only. It keeps
                // running through hit and non-hit areas without ever converting an
                // entire surface region into one continuous pattern.
                EvaluatePatternGroups(
                    branch,
                    patternCount,
                    spaceCount,
                    InvertPattern);

                evaluatedBranches.Add(branch);
            }
        }

        // ---------------------------------------------------------------------
        // Phase D: direct hit points normalize per surface. Candidate non-hit
        // points use one separate World-Z range and their own NotHitDomain.
        // ---------------------------------------------------------------------
        double[] surfaceMinimums = new double[breps.Count];
        double[] surfaceMaximums = new double[breps.Count];

        for (int i = 0; i < breps.Count; i++)
        {
            surfaceMinimums[i] = double.PositiveInfinity;
            surfaceMaximums[i] = double.NegativeInfinity;
        }

        double notHitMinimum = double.PositiveInfinity;
        double notHitMaximum = double.NegativeInfinity;

        // ---------------------------------------------------------------------
        // Phase E: output construction and one interpolated curve per branch.
        // ---------------------------------------------------------------------
        for (int branchIndex = 0; branchIndex < evaluatedBranches.Count; branchIndex++)
        {
            BranchEvaluation branch = evaluatedBranches[branchIndex];

            for (int i = 0; i < branch.Points.Count; i++)
            {
                int surfaceIndex = branch.PatternSurfaceIndices[i];

                if (!branch.KeptPatternPoints[i])
                {
                    continue;
                }

                double projection = ProjectPoint(branch.Points[i], gradientAxis);

                if (branch.HitMask[i] && surfaceIndex >= 0)
                {
                    surfaceMinimums[surfaceIndex] = Math.Min(surfaceMinimums[surfaceIndex], projection);
                    surfaceMaximums[surfaceIndex] = Math.Max(surfaceMaximums[surfaceIndex], projection);
                }
                else
                {
                    notHitMinimum = Math.Min(notHitMinimum, projection);
                    notHitMaximum = Math.Max(notHitMaximum, projection);
                }
            }
        }

        for (int branchIndex = 0; branchIndex < evaluatedBranches.Count; branchIndex++)
        {
            BranchEvaluation branch = evaluatedBranches[branchIndex];
            GH_Path path = branch.Path;

            // This is the only branch-sized point buffer used for reconstruction.
            // It preserves every original item index and replaces only indices
            // selected by the repeating pattern logic.
            Point3d[] finalBranchPoints = new Point3d[branch.Points.Count];

            hitMaskTree.EnsurePath(path);
            hitPointsTree.EnsurePath(path);
            notHitPointsTree.EnsurePath(path);
            booleanPatternTree.EnsurePath(path);
            movedPatternPointsTree.EnsurePath(path);
            movementDistancesTree.EnsurePath(path);
            surfaceIndicesTree.EnsurePath(path);
            finalPointsTree.EnsurePath(path);
            interpolatedCurvesTree.EnsurePath(path);

            IList<bool> hitMaskOutput = hitMaskTree.Branch(path);
            IList<Point3d> hitPointsOutput = hitPointsTree.Branch(path);
            IList<Point3d> notHitPointsOutput = notHitPointsTree.Branch(path);
            IList<bool> booleanPatternOutput = booleanPatternTree.Branch(path);
            IList<Point3d> movedPatternPointsOutput = movedPatternPointsTree.Branch(path);
            IList<double> movementDistancesOutput = movementDistancesTree.Branch(path);
            IList<int> surfaceIndicesOutput = surfaceIndicesTree.Branch(path);
            IList<Point3d> finalPointsOutput = finalPointsTree.Branch(path);
            IList<Curve> interpolatedCurvesOutput = interpolatedCurvesTree.Branch(path);

            for (int i = 0; i < branch.Points.Count; i++)
            {
                Point3d point = branch.Points[i];
                bool insideTolerance = branch.HitMask[i];

                hitMaskOutput.Add(insideTolerance);

                if (insideTolerance)
                {
                    hitPointsOutput.Add(point);
                }
                else
                {
                    notHitPointsOutput.Add(point);
                }

                if (branch.KeptPatternPoints[i])
                {
                    int surfaceIndex = branch.PatternSurfaceIndices[i];
                    double movementDistance;

                    if (insideTolerance && surfaceIndex >= 0)
                    {
                        movementDistance = GetHitMovementDistance(
                            point,
                            surfaceIndex,
                            gradientAxis,
                            surfaceMinimums,
                            surfaceMaximums,
                            hitMinimumDistance,
                            hitMaximumDistance,
                            breps);
                    }
                    else
                    {
                        movementDistance = GetNormalizedMovementDistance(
                            point,
                            gradientAxis,
                            notHitMinimum,
                            notHitMaximum,
                            notHitMinimumDistance,
                            notHitMaximumDistance,
                            false);
                    }
                    Point3d movedPoint =
                        point + moveDirection * movementDistance;

                    booleanPatternOutput.Add(true);
                    movedPatternPointsOutput.Add(movedPoint);
                    movementDistancesOutput.Add(movementDistance);
                    surfaceIndicesOutput.Add(surfaceIndex);
                    finalBranchPoints[i] = movedPoint;
                }
                else
                {
                    booleanPatternOutput.Add(false);
                    finalBranchPoints[i] = point;
                }

                // Always add exactly one final point for this original item index.
                finalPointsOutput.Add(finalBranchPoints[i]);
            }

            Curve interpolatedCurve =
                CreateInterpolatedBranchCurve(finalBranchPoints);

            if (interpolatedCurve != null && interpolatedCurve.IsValid)
            {
                interpolatedCurvesOutput.Add(interpolatedCurve);
            }
        }

        HitMask = hitMaskTree;
        HitPoints = hitPointsTree;
        NotHitPoints = notHitPointsTree;
        BooleanPattern = booleanPatternTree;
        MovedPatternPoints = movedPatternPointsTree;
        MovementDistances = movementDistancesTree;
        SurfaceIndices = surfaceIndicesTree;
        FinalPoints = finalPointsTree;
        InterpolatedCurves = interpolatedCurvesTree;
    }

    /// <summary>
    /// Creates one open curve from one complete, ordered FinalPoints branch.
    /// Two distinct points produce a line. Three or more points produce an
    /// interpolated curve with degree up to three. Null is returned when a usable
    /// curve cannot be made, so invalid branches never add invalid geometry.
    /// </summary>
    private Curve CreateInterpolatedBranchCurve(IList<Point3d> points)
    {
        if (points == null || points.Count < 2)
        {
            return null;
        }

        if (points.Count == 2)
        {
            if (points[0].DistanceToSquared(points[1]) <=
                RhinoMath.ZeroTolerance * RhinoMath.ZeroTolerance)
            {
                return null;
            }

            return new LineCurve(points[0], points[1]);
        }

        int degree = Math.Min(3, points.Count - 1);
        return Curve.CreateInterpolatedCurve(points, degree);
    }

    /// <summary>
    /// Marks the continuous candidate portion of the requested repeating pattern.
    /// The sequence is based only on each point's original index, so it continues
    /// through hit and non-hit regions.
    ///
    /// Exclusion is evaluated per complete PatternCount block: when any non-hit
    /// item in that block touches NotHitExclusionSurfaces, every non-hit item in
    /// the same block remains unchanged. Direct LowToHigh / HighToLow hits are
    /// deliberately unaffected by this new rule and keep their previous behavior.
    ///
    /// Loop safety: both while loops advance index and are strictly bounded by the
    /// source branch item count.
    /// </summary>
    private void EvaluatePatternGroups(
        BranchEvaluation branch,
        int patternCount,
        int spaceCount,
        bool invertPattern)
    {
        if (branch == null || branch.Points == null)
        {
            return;
        }

        int pointCount = branch.Points.Count;
        int index = 0;

        while (index < pointCount)
        {
            bool isGreen = IsGreenPatternIndex(
                index,
                patternCount,
                spaceCount,
                invertPattern);

            if (!isGreen)
            {
                index++;
                continue;
            }

            int groupStart = index;

            while (index < pointCount &&
                IsGreenPatternIndex(
                    index,
                    patternCount,
                    spaceCount,
                    invertPattern))
            {
                index++;
            }

            int groupEnd = index;
            bool excludeNotHitGroup = false;

            // One touching non-hit item removes the complete small-wave block.
            for (int i = groupStart; i < groupEnd; i++)
            {
                if (!branch.HitMask[i] && branch.NotHitExclusionMask[i])
                {
                    excludeNotHitGroup = true;
                    break;
                }
            }

            for (int i = groupStart; i < groupEnd; i++)
            {
                if (branch.HitMask[i])
                {
                    // Preserve the original hit-surface pattern behavior.
                    branch.KeptPatternPoints[i] = true;
                    branch.PatternSurfaceIndices[i] = branch.SurfaceIndices[i];
                }
                else if (!excludeNotHitGroup)
                {
                    branch.KeptPatternPoints[i] = true;
                }

                // When excludeNotHitGroup is true, non-hit items remain false and
                // FinalPoints automatically retains their original positions.
            }
        }
    }

    /// <summary>
    /// Maps one direct-hit point's World-Z position into HitDomain using
    /// the independent normalization range and explicit direction of its assigned
    /// surface. Direction comes from LowToHigh / HighToLow, after optional swapping.
    /// </summary>
    private double GetHitMovementDistance(
        Point3d point,
        int surfaceIndex,
        Vector3d gradientAxis,
        double[] surfaceMinimums,
        double[] surfaceMaximums,
        double minimumDistance,
        double maximumDistance,
        List<BrepRecord> breps)
    {
        if (surfaceIndex < 0 ||
            surfaceIndex >= surfaceMinimums.Length ||
            breps == null ||
            surfaceIndex >= breps.Count)
        {
            return minimumDistance;
        }

        double surfaceMinimum = surfaceMinimums[surfaceIndex];
        double surfaceMaximum = surfaceMaximums[surfaceIndex];
        double surfaceRange = surfaceMaximum - surfaceMinimum;
        double normalizedPosition = 0.5;

        if (surfaceRange > RhinoMath.ZeroTolerance)
        {
            double projection = ProjectPoint(point, gradientAxis);
            normalizedPosition = (projection - surfaceMinimum) / surfaceRange;
            normalizedPosition = Math.Max(0.0, Math.Min(1.0, normalizedPosition));
        }

        // Explicit direction rule:
        //   HighToLow: HighAtBottom = true  -> bottom = domain end, top = domain start.
        //   LowToHigh: HighAtBottom = false -> bottom = domain start, top = domain end.
        bool highAtBottom = breps[surfaceIndex].HighAtBottom;

        double distanceParameter = highAtBottom
            ? 1.0 - normalizedPosition  // Bottom high, top low.
            : normalizedPosition;        // Bottom low, top high.

        return minimumDistance + (maximumDistance - minimumDistance) * distanceParameter;
    }

    /// <summary>
    /// Maps a point through one independent World-Z normalization range. This is
    /// used by non-hit pattern points: bottom receives domain start and top
    /// receives domain end. The range is deliberately separate from all surfaces.
    /// </summary>
    private double GetNormalizedMovementDistance(
        Point3d point,
        Vector3d gradientAxis,
        double minimumProjection,
        double maximumProjection,
        double minimumDistance,
        double maximumDistance,
        bool highAtBottom)
    {
        double normalizedPosition = 0.5;
        double projectionRange = maximumProjection - minimumProjection;

        if (projectionRange > RhinoMath.ZeroTolerance)
        {
            double projection = ProjectPoint(point, gradientAxis);
            normalizedPosition = (projection - minimumProjection) / projectionRange;
            normalizedPosition = Math.Max(0.0, Math.Min(1.0, normalizedPosition));
        }

        double distanceParameter = highAtBottom
            ? 1.0 - normalizedPosition
            : normalizedPosition;

        return minimumDistance + (maximumDistance - minimumDistance) * distanceParameter;
    }

    /// <summary>
    /// Decodes a Grasshopper Domain into an ordered movement range. Domain
    /// direction is not used here; LowToHigh / HighToLow controls hit direction.
    /// A missing or invalid domain safely becomes the zero range.
    /// </summary>
    private void GetOrderedDomain(
        Interval domain,
        string domainName,
        out double minimumDistance,
        out double maximumDistance)
    {
        double start = domain.T0;
        double end = domain.T1;

        bool valid =
            domain.IsValid &&
            !double.IsNaN(start) &&
            !double.IsInfinity(start) &&
            !double.IsNaN(end) &&
            !double.IsInfinity(end);

        if (!valid)
        {
            minimumDistance = 0.0;
            maximumDistance = 0.0;
            Component.AddRuntimeMessage(
                GH_RuntimeMessageLevel.Warning,
                domainName + " is invalid. Using the zero movement domain.");
            return;
        }

        minimumDistance = Math.Min(start, end);
        maximumDistance = Math.Max(start, end);

        if (start > end)
        {
            Component.AddRuntimeMessage(
                GH_RuntimeMessageLevel.Warning,
                domainName + " arrived reversed and was normalized to its lower/upper values.");
        }
    }

    /// <summary>
    /// Returns the scalar projection of a point onto an axis through World origin.
    /// The current caller supplies World Z as the height/gradient axis.
    /// </summary>
    private double ProjectPoint(Point3d point, Vector3d axis)
    {
        return point.X * axis.X + point.Y * axis.Y + point.Z * axis.Z;
    }

    /// <summary>
    /// Tests whether an item lies in the candidate portion of the repeating
    /// PatternCount + SpaceCount cycle. InvertPattern places spaces first.
    /// </summary>
    private bool IsGreenPatternIndex(
        int pointIndex,
        int patternCount,
        int spaceCount,
        bool invertPattern)
    {
        if (patternCount <= 0)
        {
            return false;
        }

        int cycleLength = patternCount + spaceCount;

        if (cycleLength <= 0)
        {
            return false;
        }

        int cycleIndex = pointIndex % cycleLength;

        if (!invertPattern)
        {
            return cycleIndex < patternCount;
        }

        return cycleIndex >= spaceCount;
    }

    /// <summary>
    /// Finds the closest candidate surface from either input set within tolerance.
    /// Cached Brep boxes reject impossible candidates. Fully planar Breps use the
    /// trim-aware planar routine; only non-planar Breps use Brep.ClosestPoint.
    /// </summary>
    private bool TryGetClosestPointWithinTolerance(
        Point3d point,
        List<BrepRecord> breps,
        List<int> candidateSurfaceIndices,
        double tolerance,
        out Point3d bestClosestPoint,
        out double bestDistance,
        out int bestSurfaceIndex)
    {
        bestClosestPoint = Point3d.Unset;
        bestDistance = double.MaxValue;
        bestSurfaceIndex = -1;

        if (breps == null || breps.Count == 0 ||
            candidateSurfaceIndices == null || candidateSurfaceIndices.Count == 0)
        {
            return false;
        }

        bool found = false;
        double toleranceSquared = tolerance * tolerance;

        for (int candidateIndex = 0;
            candidateIndex < candidateSurfaceIndices.Count;
            candidateIndex++)
        {
            int surfaceIndex = candidateSurfaceIndices[candidateIndex];
            BrepRecord brepRecord = breps[surfaceIndex];

            double boundingBoxDistanceSquared = DistanceSquaredToBoundingBox(
                point,
                brepRecord.BoundingBox);

            if (boundingBoxDistanceSquared > toleranceSquared)
            {
                continue;
            }

            if (brepRecord.AllFacesPlanar)
            {
                TryGetPlanarBrepHit(
                    point,
                    brepRecord,
                    surfaceIndex,
                    tolerance,
                    toleranceSquared,
                    ref found,
                    ref bestClosestPoint,
                    ref bestDistance,
                    ref bestSurfaceIndex);

                if (bestDistance <= RhinoMath.ZeroTolerance)
                {
                    break;
                }

                continue;
            }

            Brep brep = brepRecord.Brep;

            if (brep == null)
            {
                continue;
            }

            Point3d closestPoint = brep.ClosestPoint(point);

            if (!closestPoint.IsValid)
            {
                continue;
            }

            double distance = point.DistanceTo(closestPoint);

            if (distance <= tolerance && distance < bestDistance)
            {
                bestDistance = distance;
                bestClosestPoint = closestPoint;
                bestSurfaceIndex = surfaceIndex;
                found = true;

                if (bestDistance <= RhinoMath.ZeroTolerance)
                {
                    break;
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Performs a tolerance- and trim-aware hit test on every planar face in one
    /// BrepRecord. Box and plane checks occur before UV/trim work. PlaneSurface
    /// faces use direct parameters; other planar faces use a safe fallback.
    /// </summary>
    private void TryGetPlanarBrepHit(
        Point3d point,
        BrepRecord brepRecord,
        int surfaceIndex,
        double tolerance,
        double toleranceSquared,
        ref bool found,
        ref Point3d bestClosestPoint,
        ref double bestDistance,
        ref int bestSurfaceIndex)
    {
        for (int faceIndex = 0; faceIndex < brepRecord.Faces.Count; faceIndex++)
        {
            FaceRecord faceRecord = brepRecord.Faces[faceIndex];

            if (DistanceSquaredToBoundingBox(point, faceRecord.BoundingBox) >
                toleranceSquared)
            {
                continue;
            }

            double planeDistance = Math.Abs(faceRecord.Plane.DistanceTo(point));

            if (planeDistance > tolerance || planeDistance >= bestDistance)
            {
                continue;
            }

            double u;
            double v;

            bool foundParameters;

            if (faceRecord.ParameterSurface != null)
            {
                foundParameters = faceRecord.ParameterSurface.Plane.ClosestParameter(
                    point,
                    out u,
                    out v);
            }
            else
            {
                foundParameters = faceRecord.Face.ClosestPoint(
                    point,
                    out u,
                    out v);
            }

            if (!foundParameters)
            {
                continue;
            }

            PointFaceRelation relation = faceRecord.Face.IsPointOnFace(
                u,
                v,
                tolerance);

            if (relation == PointFaceRelation.Exterior)
            {
                continue;
            }

            Point3d closestPoint = faceRecord.Face.PointAt(u, v);

            if (!closestPoint.IsValid)
            {
                continue;
            }

            double distance = point.DistanceTo(closestPoint);

            if (distance <= tolerance && distance < bestDistance)
            {
                bestDistance = distance;
                bestClosestPoint = closestPoint;
                bestSurfaceIndex = surfaceIndex;
                found = true;
            }
        }
    }

    /// <summary>
    /// Returns combined surface indices whose boxes can touch the current branch.
    /// This branch-level broad phase avoids testing every point against every Brep.
    /// </summary>
    private List<int> GetCandidateSurfaceIndices(
        IList<Point3d> branchPoints,
        List<BrepRecord> breps,
        double tolerance)
    {
        List<int> candidates = new List<int>();

        if (branchPoints == null || branchPoints.Count == 0 ||
            breps == null || breps.Count == 0)
        {
            return candidates;
        }

        BoundingBox branchBoundingBox = GetPointBoundingBox(branchPoints);
        double toleranceSquared = tolerance * tolerance;

        for (int i = 0; i < breps.Count; i++)
        {
            if (DistanceSquaredBetweenBoundingBoxes(
                branchBoundingBox,
                breps[i].BoundingBox) <= toleranceSquared)
            {
                candidates.Add(i);
            }
        }

        return candidates;
    }

    /// <summary>
    /// Computes a branch bounding box without copying the point collection.
    /// Invalid points are ignored; no valid points returns BoundingBox.Empty.
    /// </summary>
    private BoundingBox GetPointBoundingBox(IList<Point3d> points)
    {
        Point3d minimum = new Point3d(
            double.PositiveInfinity,
            double.PositiveInfinity,
            double.PositiveInfinity);
        Point3d maximum = new Point3d(
            double.NegativeInfinity,
            double.NegativeInfinity,
            double.NegativeInfinity);
        bool foundValidPoint = false;

        for (int i = 0; i < points.Count; i++)
        {
            Point3d point = points[i];

            if (!point.IsValid)
            {
                continue;
            }

            minimum.X = Math.Min(minimum.X, point.X);
            minimum.Y = Math.Min(minimum.Y, point.Y);
            minimum.Z = Math.Min(minimum.Z, point.Z);
            maximum.X = Math.Max(maximum.X, point.X);
            maximum.Y = Math.Max(maximum.Y, point.Y);
            maximum.Z = Math.Max(maximum.Z, point.Z);
            foundValidPoint = true;
        }

        return foundValidPoint
            ? new BoundingBox(minimum, maximum)
            : BoundingBox.Empty;
    }

    /// <summary>
    /// Returns squared minimum separation between two axis-aligned boxes.
    /// Squared distances avoid unnecessary square roots in broad-phase tests.
    /// </summary>
    private double DistanceSquaredBetweenBoundingBoxes(
        BoundingBox first,
        BoundingBox second)
    {
        if (!first.IsValid || !second.IsValid)
        {
            return double.MaxValue;
        }

        double dx = IntervalGap(
            first.Min.X,
            first.Max.X,
            second.Min.X,
            second.Max.X);
        double dy = IntervalGap(
            first.Min.Y,
            first.Max.Y,
            second.Min.Y,
            second.Max.Y);
        double dz = IntervalGap(
            first.Min.Z,
            first.Max.Z,
            second.Min.Z,
            second.Max.Z);

        return dx * dx + dy * dy + dz * dz;
    }

    /// <summary>
    /// Returns the non-negative gap between two 1D intervals; overlap returns zero.
    /// </summary>
    private double IntervalGap(
        double firstMinimum,
        double firstMaximum,
        double secondMinimum,
        double secondMaximum)
    {
        if (firstMaximum < secondMinimum)
        {
            return secondMinimum - firstMaximum;
        }

        if (secondMaximum < firstMinimum)
        {
            return firstMinimum - secondMaximum;
        }

        return 0.0;
    }

    /// <summary>
    /// Returns squared minimum point-to-box distance for point-level broad phase.
    /// </summary>
    private double DistanceSquaredToBoundingBox(
        Point3d point,
        BoundingBox boundingBox)
    {
        if (!boundingBox.IsValid)
        {
            return 0.0;
        }

        double dx = 0.0;
        double dy = 0.0;
        double dz = 0.0;

        if (point.X < boundingBox.Min.X)
        {
            dx = boundingBox.Min.X - point.X;
        }
        else if (point.X > boundingBox.Max.X)
        {
            dx = point.X - boundingBox.Max.X;
        }

        if (point.Y < boundingBox.Min.Y)
        {
            dy = boundingBox.Min.Y - point.Y;
        }
        else if (point.Y > boundingBox.Max.Y)
        {
            dy = point.Y - boundingBox.Max.Y;
        }

        if (point.Z < boundingBox.Min.Z)
        {
            dz = boundingBox.Min.Z - point.Z;
        }
        else if (point.Z > boundingBox.Max.Z)
        {
            dz = point.Z - boundingBox.Max.Z;
        }

        return dx * dx + dy * dy + dz * dz;
    }

    /// <summary>
    /// Per-surface cache created once per solution. It keeps the original Brep,
    /// broad-phase box, cached face records, planar status, and explicit gradient
    /// direction. Its position in the combined list is the public surface index.
    /// </summary>
    private class BrepRecord
    {
        public Brep Brep;
        public BoundingBox BoundingBox;
        public List<FaceRecord> Faces;
        public bool AllFacesPlanar;
        // True: HighToLow  (bottom high, top low).
        // False: LowToHigh (bottom low, top high).
        public bool HighAtBottom;

        public BrepRecord(
            Brep brep,
            bool highAtBottom,
            double tolerance)
        {
            Brep = brep;
            HighAtBottom = highAtBottom;
            BoundingBox = brep.GetBoundingBox(false);
            Faces = new List<FaceRecord>();
            AllFacesPlanar = brep.Faces.Count > 0;

            for (int i = 0; i < brep.Faces.Count; i++)
            {
                BrepFace face = brep.Faces[i];
                Plane plane;
                bool isPlanar = face.TryGetPlane(out plane, tolerance);

                Faces.Add(new FaceRecord(face, plane));
                AllFacesPlanar = AllFacesPlanar && isPlanar;
            }
        }
    }

    /// <summary>
    /// Per-face planar-test cache. ParameterSurface is non-null only when direct,
    /// inexpensive plane-to-UV conversion is safe; otherwise the face fallback is
    /// used. The face box rejects most points before trim classification.
    /// </summary>
    private class FaceRecord
    {
        public BrepFace Face;
        public Plane Plane;
        public PlaneSurface ParameterSurface;
        public BoundingBox BoundingBox;

        public FaceRecord(BrepFace face, Plane plane)
        {
            Face = face;
            Plane = plane;
            ParameterSurface = face.UnderlyingSurface() as PlaneSurface;
            BoundingBox = face.GetBoundingBox(false);
        }
    }

    /// <summary>
    /// Full-index internal state for one Points branch. Every array has exactly the
    /// same length as Points, so item i always refers to the same original point.
    ///
    /// SurfaceIndices stores the direct-hit surface or -1.
    /// NotHitExclusionMask records individual non-hit contact with the optional
    /// exclusion surfaces. EvaluatePatternGroups expands any such contact across
    /// the complete non-hit PatternCount block.
    /// KeptPatternPoints stores the continuous repeating-pattern mask.
    /// PatternSurfaceIndices stores a direct-hit surface or -1 for non-hit items.
    /// </summary>
    private class BranchEvaluation
    {
        public GH_Path Path;
        public IList<Point3d> Points;
        public bool[] HitMask;
        public bool[] NotHitExclusionMask;
        public double[] Distances;
        public int[] SurfaceIndices;
        public bool[] KeptPatternPoints;
        public int[] PatternSurfaceIndices;

        public BranchEvaluation(GH_Path path, IList<Point3d> points)
        {
            Path = path;
            Points = points;

            int count = points.Count;
            HitMask = new bool[count];
            NotHitExclusionMask = new bool[count];
            Distances = new double[count];
            SurfaceIndices = new int[count];
            KeptPatternPoints = new bool[count];
            PatternSurfaceIndices = new int[count];

            for (int i = 0; i < count; i++)
            {
                SurfaceIndices[i] = -1;
                PatternSurfaceIndices[i] = -1;
            }
        }
    }

    /// <summary>
    /// Combines both optional surface trees into one deterministic indexed list.
    /// LowToHigh surfaces are appended first, followed by HighToLow surfaces.
    /// SwapInputSurfaces reverses their gradient behavior without changing indices.
    /// </summary>
    private List<BrepRecord> CollectBreps(
        DataTree<Brep> lowToHighTree,
        DataTree<Brep> highToLowTree,
        bool swapInputSurfaces,
        double tolerance)
    {
        List<BrepRecord> breps = new List<BrepRecord>();

        // Normal behavior:
        //   LowToHigh  -> bottom low,  top high (HighAtBottom = false)
        //   HighToLow  -> bottom high, top low  (HighAtBottom = true)
        // SwapInputSurfaces reverses these two behavior flags only.
        AppendBreps(
            breps,
            lowToHighTree,
            swapInputSurfaces,
            tolerance);
        AppendBreps(
            breps,
            highToLowTree,
            !swapInputSurfaces,
            tolerance);

        return breps;
    }

    /// <summary>
    /// Appends all valid Breps from one optional tree. A null or empty tree is a
    /// valid input and simply contributes no surfaces.
    /// </summary>
    private void AppendBreps(
        List<BrepRecord> destination,
        DataTree<Brep> brepTree,
        bool highAtBottom,
        double tolerance)
    {
        if (brepTree == null)
        {
            return;
        }

        for (int branchIndex = 0; branchIndex < brepTree.BranchCount; branchIndex++)
        {
            IList<Brep> branch = brepTree.Branch(branchIndex);

            if (branch == null)
            {
                continue;
            }

            for (int i = 0; i < branch.Count; i++)
            {
                Brep brep = branch[i];

                if (brep != null)
                {
                    destination.Add(new BrepRecord(
                        brep,
                        highAtBottom,
                        tolerance));
                }
            }
        }
    }
}
