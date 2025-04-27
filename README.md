# StableShape

Stable fluid simulation is a computational fluid dynamics method proposed by Jos Stam in 1999. The core advantage of this method lies in its unconditional stability, which means it is less prone to numerical instability issues when simulating complex fluid behaviors. Stam's method simplified the process for animators to create fluid simulations, enabling them to more easily generate complex and realistic fluid effects. This technique has been widely applied in various fields, including game development and visual effects.

This plugin contains the StableFluid core solver, a mesh processing solver, and pre/post-processing components. Designers can use custom forces and initial density points to input into the core solver to calculate velocity fields and density fields.

Use Trigger to continuously update velocity and density fields. The velocity field can be input into the grid solver to affect mesh vertices, simulating the effect of fluid impact on the mesh.

Feel free to Twist your mesh!
