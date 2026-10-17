"""Small local cleanup of the approved floral v4 mask."""

from pathlib import Path

import cv2
import numpy as np

root = Path(__file__).resolve().parent
source = root / "floral-pattern-uv-518.68x457.20-seamless-v4.png"
output = root / "floral-pattern-uv-518.68x457.20-seamless-v5.png"
image = cv2.imread(str(source), cv2.IMREAD_GRAYSCALE)
assert image is not None

# Fill the long white slit between the two branches and the tiny enclosed
# white pinhole without disturbing the surrounding white background.
_, white_labels = cv2.connectedComponents((image >= 128).astype(np.uint8))
for x, y in ((640, 275), (565, 629)):
    image[white_labels == white_labels[y, x]] = 0

# Remove the detached black fleck below the central leaves.
_, black_labels = cv2.connectedComponents((image < 128).astype(np.uint8))
image[black_labels == black_labels[1052, 768]] = 255

# Close the narrow open wedge where the two bottom leaves join the branch.
bridge = np.array(
    [
        (566, 1049), (587, 1040), (610, 1040), (630, 1053),
        (645, 1076), (664, 1102), (697, 1133), (662, 1122),
        (629, 1090), (602, 1066), (580, 1060),
    ],
    dtype=np.int32,
)
cv2.fillPoly(image, [bridge], 0)
image[1125, 669] = 0  # Clear a one-pixel white remnant inside the join.

assert np.array_equal(image[:, 0], image[:, -1])
assert cv2.imwrite(str(output), image)
print(output)
