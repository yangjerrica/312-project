import socket
import matplotlib.pyplot as plt
from collections import deque
import threading
import time

# Set up UDP server to listen for Unity data
UDP_IP = "10.0.0.201"  # IP Address of the machine where the Python script is running
UDP_PORT = 8051

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.bind((UDP_IP, UDP_PORT))

# Joint names
joint_names = [
    "outer_yaw_joint",
    "outer_pitch_joint",
    "outer_insertion_joint",
    "outer_roll_joint",
    "outer_wrist_pitch_joint",
    "outer_wrist_yaw_joint"
]

# Data storage for each joint
max_data_points = 100
joint_data = {joint: deque([0.0] * max_data_points, maxlen=max_data_points) for joint in joint_names}
time_data = deque([time.time() - max_data_points + i for i in range(max_data_points)], maxlen=max_data_points)

# Function to receive data and update joint values
def receive_data():
    while True:
        data, addr = sock.recvfrom(1024)  # Buffer size is 1024 bytes
        message = data.decode('utf-8')
        print(f"Received message: {message}")
        try:
            # Parse the joint name and value
            joint_name, joint_value = message.split(',')
            joint_value = float(joint_value)

            if joint_name in joint_data:
                # Update the data storage for the respective joint
                joint_data[joint_name].append(joint_value)

                # Update the time data for all joints (assuming they all share the same timeline)
                time_data.append(time.time())
        except ValueError:
            print("Failed to parse the received message.")

# Start a thread for receiving UDP data
receiver_thread = threading.Thread(target=receive_data, daemon=True)
receiver_thread.start()

# Plotting in real-time
plt.ion()  # Interactive mode on
fig, axs = plt.subplots(2, 3, figsize=(11.25, 6), sharex=True)  # 2x3 grid for the six joints, 25% smaller size
lines = []

# Set up each subplot in the 2x3 grid
for i, joint_name in enumerate(joint_names):
    row, col = divmod(i, 3)  # Determine row and column based on the index
    ax = axs[row, col]
    line, = ax.plot(time_data, joint_data[joint_name], label=joint_name)
    lines.append(line)
    ax.set_ylabel('Joint Value')
    ax.set_title(f'Real-time Data for {joint_name}')
    ax.legend()

# Hide any empty subplots (if there are fewer joints than grid spaces)
for i in range(len(joint_names), 6):
    row, col = divmod(i, 3)
    fig.delaxes(axs[row, col])

plt.xlabel('Time (s)', fontsize=12)
fig.text(0.5, 0.04, 'Time (s)', ha='center', fontsize=14)  # Shared x-axis label
fig.tight_layout(rect=[0, 0.05, 1, 0.95])  # Adjust layout to add space for the shared x-axis label

# Loop to update the plots in real-time
while True:
    try:
        for i, joint_name in enumerate(joint_names):
            row, col = divmod(i, 3)
            lines[i].set_xdata(time_data)
            lines[i].set_ydata(joint_data[joint_name])
            axs[row, col].relim()
            axs[row, col].autoscale_view()

        # Pause for a short interval to update the plot
        plt.pause(0.05)
    except KeyboardInterrupt:
        print("Stopping the plotting...")
        break

plt.ioff()
plt.show()
