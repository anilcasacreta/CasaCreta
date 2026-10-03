import Rhino.Geometry as rg
import ghpythonlib.components as ghc
import math

# =========================
# 1. Series calculation
# =========================
def generate_series(srf_ref, layer_height, bottom_layers, box_h, plain_layers_contours):
    
    # start
    sr_start = layer_height * bottom_layers
    
    # step
    sr_step = (layer_height * plain_layers_contours) + box_h
    
    # bounding box height
    bbox = srf_ref.GetBoundingBox(True)
    srf_z_height = bbox.Max.Z - bbox.Min.Z
    
    # count
    sr_count_float = srf_z_height / layer_height
    sr_count = int(math.ceil(sr_count_float))
    
    # series (GH component)
    series = ghc.Series(sr_start, sr_step, sr_count)
    
    return series, sr_start, sr_step, sr_count


# =========================
# 2. Plane creation
# =========================
def get_xy_plane():
    return rg.Plane.WorldXY


# =========================
# 3. Box dimension logic
# =========================
def get_box_dimensions(box_h, box_w, width_as_height):
    
    if width_as_height:
        return box_h, box_h
    else:
        return box_h, box_w


# =========================
# MAIN EXECUTION
# =========================

# series
series, sr_start, sr_step, sr_count = generate_series(
    srf_ref, layer_height, bottom_layers, box_h, plain_layers_contours
    
)

# plane
plane = get_xy_plane()

# box dimensions
box_height, box_width = get_box_dimensions(box_h, box_w, width_as_height)

# =========================
# outputs
# =========================
offsets = series
XYplane = plane
shape = srf_ref

gate = width_as_height