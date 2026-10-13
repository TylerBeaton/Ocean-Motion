"""Small minimum-norm linear pose solver; degrees are the joint input unit."""

def solve_min_norm(columns, target, damping=0.01):
    """Solve J * joint_degrees ~= target with three measured pose rows."""
    if len(target) != 3 or any(len(column) != 3 for column in columns):
        raise ValueError("Expected three pose axes per joint")
    if damping < 0 or not columns:
        raise ValueError("Invalid damping or empty joint list")
    normal = [[sum(column[row] * column[col] for column in columns) +
               (damping if row == col else 0.0) for col in range(3)]
              for row in range(3)]
    augmented = [normal[row] + [target[row]] for row in range(3)]
    for pivot in range(3):
        best = max(range(pivot, 3), key=lambda row: abs(augmented[row][pivot]))
        augmented[pivot], augmented[best] = augmented[best], augmented[pivot]
        diagonal = augmented[pivot][pivot]
        if abs(diagonal) < 1e-12:
            raise ValueError("Joint response cannot span three pose axes")
        for row in range(pivot + 1, 3):
            factor = augmented[row][pivot] / diagonal
            for column in range(pivot, 4):
                augmented[row][column] -= factor * augmented[pivot][column]
    solution = [0.0] * 3
    for row in range(2, -1, -1):
        solution[row] = (augmented[row][3] - sum(
            augmented[row][col] * solution[col] for col in range(row + 1, 3))) / augmented[row][row]
    return tuple(sum(column[axis] * solution[axis] for axis in range(3)) for column in columns)
